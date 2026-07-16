using System.Text.Json;
using StackExchange.Redis;

namespace HelloLogging.Caching;

/// <summary>
/// Minimal string cache abstraction so the cache-aside logic can be tested
/// without a running Redis instance.
/// </summary>
public interface ICacheStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value, TimeSpan ttl);
}

public sealed class RedisCacheStore(IConnectionMultiplexer redis) : ICacheStore
{
    public async Task<string?> GetAsync(string key)
    {
        var value = await redis.GetDatabase().StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    // SET with an expiry writes the value and its TTL in one atomic command,
    // so a key can never linger without an expiration timer.
    public Task SetAsync(string key, string value, TimeSpan ttl) =>
        redis.GetDatabase().StringSetAsync(key, value, ttl);
}

public static class CacheKeys
{
    // Keys are namespaced app:purpose:resource so they stay greppable and
    // deletable by prefix, since a key-value store can only look up exact keys.
    public const string Products = "hellologging:cache:products";
}

public static class ResponseCache
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Cache-aside: return the cached value when present; otherwise invoke the
    /// origin, store its result under <paramref name="key"/> with the TTL, and
    /// return it. Cached responses may be up to one TTL stale by design.
    /// </summary>
    public static async Task<(T Value, bool FromCache)> GetOrCreateAsync<T>(
        ICacheStore store,
        string key,
        TimeSpan ttl,
        Func<Task<T>> origin)
    {
        var cached = await store.GetAsync(key);
        if (cached is not null)
        {
            var cachedValue = JsonSerializer.Deserialize<T>(cached);
            if (cachedValue is not null)
            {
                return (cachedValue, true);
            }
        }

        var value = await origin();
        await store.SetAsync(key, JsonSerializer.Serialize(value), ttl);
        return (value, false);
    }
}
