using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Order.API.Middleware;
using Order.Infrastructure.Persistence;

namespace E2E.Tests;

// Fábrica de teste do Order.API que mantém o BackgroundService do RabbitMQ (CreateOrderConsumer)
// realmente rodando, apontando para o broker do Testcontainers, e usa SQLite em memória
// só para não depender de um arquivo em disco. É por isso que este teste é "ponta a ponta":
// diferente do CustomWebApplicationFactory usado pelos testes de integração do Order.API,
// aqui o consumer processa mensagens de verdade.
//
// Usamos ExceptionHandlingMiddleware (em vez de Program) como âncora de assembly porque
// tanto Order.API quanto Gateway.API geram uma classe "Program" a partir de top-level
// statements; referenciar as duas no mesmo projeto de teste causaria ambiguidade (CS0433).
// WebApplicationFactory<T> só precisa de um tipo público qualquer do assembly alvo.
public class OrderApiFactory(string rabbitMqHost, int rabbitMqPort) : WebApplicationFactory<ExceptionHandlingMiddleware>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = rabbitMqHost,
                ["RabbitMq:Port"] = rabbitMqPort.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.AddDbContext<OrderDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            dbContext.Database.Migrate();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
