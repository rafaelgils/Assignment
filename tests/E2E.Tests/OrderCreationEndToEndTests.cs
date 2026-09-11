using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gateway.API.Auth;
using Gateway.API.Clients;
using Gateway.API.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Contracts.Messaging;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace E2E.Tests;

// Teste ponta a ponta real: sobe RabbitMQ e Redis de verdade (Testcontainers), hospeda o
// Gateway.API e o Order.API em memória (WebApplicationFactory) apontando para esses
// containers, e exercita o fluxo completo por HTTP:
//   login -> POST /api/order (Gateway publica no RabbitMQ) -> CreateOrderConsumer do
//   Order.API processa a mensagem -> GET /api/orders/{id} (via Gateway) confirma o pedido.
// Requer Docker rodando; não usa nenhum mock/fake para RabbitMQ, Redis ou HTTP entre os
// dois serviços.
public class OrderCreationEndToEndTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    private RabbitMqContainer _rabbitMq = null!;
    private OrderApiFactory _orderApiFactory = null!;
    private GatewayApiFactory _gatewayApiFactory = null!;
    private string _fixedUserEmail = string.Empty;
    private string _fixedUserPassword = string.Empty;

    public async Task InitializeAsync()
    {
        // Lê usuário/senha do RabbitMQ e do usuário fixo direto do appsettings.json real
        // do Gateway, em vez de duplicar esses valores como literais no teste.
        var (rabbitMqUser, rabbitMqPassword, fixedUser) = ReadGatewayDefaults();
        _fixedUserEmail = fixedUser.Email;
        _fixedUserPassword = fixedUser.Password;

        _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management")
            .WithUsername(rabbitMqUser)
            .WithPassword(rabbitMqPassword)
            .Build();

        await Task.WhenAll(_rabbitMq.StartAsync(), _redis.StartAsync());

        _orderApiFactory = new OrderApiFactory(_rabbitMq.Hostname, _rabbitMq.GetMappedPublicPort(5672));

        _gatewayApiFactory = new GatewayApiFactory(
            _rabbitMq.Hostname,
            _rabbitMq.GetMappedPublicPort(5672),
            _redis.GetConnectionString(),
            _orderApiFactory);
    }

    private static (string RabbitMqUser, string RabbitMqPassword, FixedUserOptions FixedUser) ReadGatewayDefaults()
    {
        // Fábrica descartável, sem containers: só para ler os IOptions<T> já vinculados
        // pelo appsettings.json/environment reais do Gateway.API. Nenhuma conexão de rede
        // é aberta por isso (os singletons de Redis/RabbitMQ só conectam sob demanda).
        using var factory = new WebApplicationFactory<AuthController>();

        var rabbitMq = factory.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        var fixedUser = factory.Services.GetRequiredService<IOptions<FixedUserOptions>>().Value;

        return (rabbitMq.UserName, rabbitMq.Password, fixedUser);
    }

    public async Task DisposeAsync()
    {
        _gatewayApiFactory.Dispose();
        _orderApiFactory.Dispose();
        await _rabbitMq.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Creates_an_order_through_the_gateway_and_makes_it_available_via_the_order_api()
    {
        var client = _gatewayApiFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, _fixedUserEmail, _fixedUserPassword));

        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            [new CreateOrderItemRequest("Widget", 2, 9.99m)]);

        using var createOrderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/order")
        {
            Content = JsonContent.Create(request)
        };
        createOrderRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var createResponse = await client.SendAsync(createOrderRequest);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        var accepted = await createResponse.Content.ReadFromJsonAsync<AcceptedOrderResponse>();
        Assert.NotNull(accepted);

        var order = await WaitForOrderAsync(client, accepted!.Id, TimeSpan.FromSeconds(20));

        Assert.Equal(accepted.Id, order.Id);
        Assert.Equal(request.CustomerId, order.CustomerId);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(19.98m, order.TotalAmount);
        Assert.Single(order.Items);
        Assert.Equal("Widget", order.Items[0].ProductName);
    }

    [Fact]
    public async Task Resending_the_same_idempotency_key_does_not_create_a_second_order()
    {
        var client = _gatewayApiFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, _fixedUserEmail, _fixedUserPassword));

        var request = new CreateOrderRequest(Guid.NewGuid(), [new CreateOrderItemRequest("Gadget", 1, 5m)]);
        var idempotencyKey = Guid.NewGuid().ToString();

        var firstId = await SendCreateOrderAsync(client, request, idempotencyKey);
        var secondId = await SendCreateOrderAsync(client, request, idempotencyKey);

        Assert.Equal(firstId, secondId);
        await WaitForOrderAsync(client, firstId, TimeSpan.FromSeconds(20));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private static async Task<Guid> SendCreateOrderAsync(HttpClient client, CreateOrderRequest request, string idempotencyKey)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/order")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedOrderResponse>();
        return accepted!.Id;
    }

    private static async Task<OrderModel> WaitForOrderAsync(HttpClient client, Guid orderId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (true)
        {
            var response = await client.GetAsync($"/api/orders/{orderId}", cts.Token);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return (await response.Content.ReadFromJsonAsync<OrderModel>(cts.Token))!;
            }

            await Task.Delay(250, cts.Token);
        }
    }

    private record AcceptedOrderResponse(Guid Id);
}
