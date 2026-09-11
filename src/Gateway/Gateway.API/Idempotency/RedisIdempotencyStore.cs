using StackExchange.Redis;

namespace Gateway.API.Idempotency;

public class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore
{
    //Tempo de vida padrão para as chaves de idempotência no Redis.
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    //Gera a chave completa para armazenar a idempotência no Redis.
    private static string Key(string idempotencyKey) => $"idempotency:order:{idempotencyKey}";

    //Tenta obter o ID do pedido associado à chave de idempotência.
    public async Task<Guid?> TryGetAsync(string idempotencyKey)
    {
        var value = await redis.GetDatabase().StringGetAsync(Key(idempotencyKey));
        return value.HasValue && Guid.TryParse((string?)value, out var orderId) ? orderId : null;
    }

    //Salva o ID do pedido associado à chave de idempotência.
    public Task SaveAsync(string idempotencyKey, Guid orderId) =>
        redis.GetDatabase().StringSetAsync(Key(idempotencyKey), orderId.ToString(), Ttl);
}
