namespace Gateway.API.Idempotency;

public interface IIdempotencyStore
{
    Task<Guid?> TryGetAsync(string idempotencyKey);
    Task SaveAsync(string idempotencyKey, Guid orderId);
}
