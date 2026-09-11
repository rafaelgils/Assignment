using StackExchange.Redis;

namespace Gateway.API.Auth;

public interface ITokenCacheService
{
    Task StoreActiveTokenAsync(string jti, DateTime expiresAtUtc);
    Task<bool> IsTokenActiveAsync(string jti);
}

public class TokenCacheService(IConnectionMultiplexer redis) : ITokenCacheService
{
    private static string Key(string jti) => $"auth:token:{jti}";

    public Task StoreActiveTokenAsync(string jti, DateTime expiresAtUtc)
    {
        var ttl = expiresAtUtc - DateTime.UtcNow;
        return redis.GetDatabase().StringSetAsync(Key(jti), "active", ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1));
    }

    public async Task<bool> IsTokenActiveAsync(string jti)
    {
        var value = await redis.GetDatabase().StringGetAsync(Key(jti));
        return value.HasValue;
    }
}
