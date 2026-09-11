using System.Net;
using System.Net.Http.Json;

namespace Gateway.API.Clients;

public class OrderApiClient(HttpClient httpClient) : IOrderApiClient
{
    public async Task<PagedOrdersModel> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<PagedOrdersModel>(
            $"orders?page={page}&pageSize={pageSize}", cancellationToken);

        return result ?? new PagedOrdersModel { Page = page, PageSize = pageSize };
    }

    public async Task<OrderModel?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"orders/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderModel>(cancellationToken);
    }

    public async Task<CancelOrderResult> CancelOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"orders/{id}/cancel", content: null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CancelOrderResult.NotFound;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return CancelOrderResult.Conflict;
        }

        response.EnsureSuccessStatusCode();
        return CancelOrderResult.Cancelled;
    }
}
