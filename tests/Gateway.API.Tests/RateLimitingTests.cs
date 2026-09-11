using System.Net;
using System.Net.Http.Json;
using Gateway.API.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gateway.API.Tests;

public class RateLimitingTests
{
    private static readonly LoginRequest WrongCredentials = new("nobody@example.com", "wrong-password");

    [Fact]
    public async Task Login_is_blocked_after_the_configured_limit_is_reached()
    {
        // Limite global bem alto para isolar o teste na política específica do "login".
        using var factory = new GatewayFactory(globalPermitLimit: 1000, loginPermitLimit: 3);
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/auth/login", WrongCredentials);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/auth/login", WrongCredentials);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    [Fact]
    public async Task Health_endpoint_is_blocked_by_the_global_limit()
    {
        // Limite de login bem alto para isolar o teste no limite global (o /health não tem política própria).
        using var factory = new GatewayFactory(globalPermitLimit: 3, loginPermitLimit: 1000);
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var blocked = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    private class GatewayFactory(int globalPermitLimit, int loginPermitLimit) : WebApplicationFactory<AuthController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:PermitLimit"] = globalPermitLimit.ToString(),
                    ["RateLimiting:WindowSeconds"] = "60",
                    ["RateLimiting:QueueLimit"] = "0",
                    ["RateLimiting:LoginPermitLimit"] = loginPermitLimit.ToString(),
                    ["RateLimiting:LoginWindowSeconds"] = "60",
                    // abortConnect=false: o AuthController injeta ITokenCacheService (que depende
                    // do IConnectionMultiplexer) mesmo no caminho de credenciais inválidas, então
                    // a conexão precisa "suceder" sem exigir um Redis real disponível no teste.
                    ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
                });
            });
        }
    }
}
