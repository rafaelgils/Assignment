using System.Text;
using System.Threading.RateLimiting;
using Gateway.API.Auth;
using Gateway.API.Clients;
using Gateway.API.Idempotency;
using Gateway.API.Messaging;
using Gateway.API.RateLimiting;
using Gateway.API.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Serilog;
using Shared.Contracts.Messaging;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddOpenApi();
builder.Services.AddControllers();

//Configurando o JWT, usuário fixo e RabbitMQ a partir das seções de configuração
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<FixedUserOptions>(builder.Configuration.GetSection(FixedUserOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

//Adicionando os singletons que precisam ser unicos no contexto da aplicação
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSingleton<RabbitMqConnectionFactory>();
builder.Services.AddSingleton<IOrderMessagePublisher, RabbitMqOrderMessagePublisher>();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ITokenCacheService, TokenCacheService>();
builder.Services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

builder.Services.AddSingleton<IAuthorizationHandler, ActiveTokenAuthorizationHandler>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveToken", policy => policy.Requirements.Add(new ActiveTokenRequirement()));
});

builder.Services.AddHttpClient<IOrderApiClient, OrderApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderApi"]!);
})
    .AddPolicyHandler(ResiliencePolicies.RetryPolicy())
    .AddPolicyHandler(ResiliencePolicies.CircuitBreakerPolicy());

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

// Rate limiting para mitigar DDoS/flood: um limite global por IP em todos os endpoints,
// mais uma política extra e mais restrita só para /auth/login (alvo comum de brute-force).
// As opções são lidas de httpContext.RequestServices (não de uma variável capturada aqui em
// cima) porque essa configuração roda antes de builder.Build(): capturar o valor cedo demais
// "congela" appsettings.json e ignora qualquer override de configuração feito depois (ex.: em
// testes com WebApplicationFactory).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Muitas requisições. Tente novamente em instantes." },
            cancellationToken);
    };

    static string ClientKey(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var rateLimiting = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(ClientKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimiting.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
            QueueLimit = rateLimiting.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.AddPolicy("login", httpContext =>
    {
        var rateLimiting = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(ClientKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimiting.LoginPermitLimit,
            Window = TimeSpan.FromSeconds(rateLimiting.LoginWindowSeconds),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Gateway.API" }));

app.MapControllers();

app.Run();
