using Gateway.API.Clients;
using Gateway.API.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Tests;

// Fábrica de teste do Gateway.API que aponta o Redis/RabbitMQ reais (Testcontainers) e
// redireciona o HttpClient usado para falar com o Order.API para o TestServer em memória
// da OrderApiFactory, em vez de uma porta de rede real — assim as duas APIs conversam
// de verdade (via RabbitMQ), sem precisar dar "dotnet run" nelas.
//
// Usamos AuthController (em vez de Program) como âncora de assembly pelo mesmo motivo
// documentado em OrderApiFactory: evitar a ambiguidade entre os dois "Program" gerados.
public class GatewayApiFactory(
    string rabbitMqHost,
    int rabbitMqPort,
    string redisConnectionString,
    OrderApiFactory orderApiFactory) : WebApplicationFactory<AuthController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = rabbitMqHost,
                ["RabbitMq:Port"] = rabbitMqPort.ToString(),
                ["ConnectionStrings:Redis"] = redisConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<IOrderApiClient, OrderApiClient>(client =>
                {
                    client.BaseAddress = orderApiFactory.Server.BaseAddress;
                })
                .ConfigurePrimaryHttpMessageHandler(() => orderApiFactory.Server.CreateHandler());
        });
    }
}
