# W2 Wednesday — Redis Cache Layer (60s TTL) Deliverable Summary

## What Was Built

Added a Redis **cache-aside** layer to the `HelloLogging` ASP.NET Core API. A new `GET /products` endpoint serves a deliberately slow "origin" (a 500 ms simulated upstream call) through a Redis cache with a **60-second TTL**. The first request is a cache MISS that hits the origin and stores the JSON under a namespaced key; subsequent requests within 60 seconds are cache HITs served from Redis in a few milliseconds. This reuses the Redis `IConnectionMultiplexer` already wired in Week 1 — no new packages or compose changes.

## Course concepts applied (*NoSQL Foundations: Key-value Databases*)

- **Cache-aside pattern** — the course's named e-commerce example ("check Redis first; if the key is found, it skips the database entirely"). `GET /products` checks Redis, and only calls the origin on a miss, then populates the cache.
- **TTL** — "set an expiration timer on any key… automatically deleted, no manual cleanup." The value is written with `StringSetAsync(key, value, TimeSpan.FromSeconds(60))`, so expiry is atomic with the write and self-cleaning.
- **Key namespacing** — mirrors the course's `user:123` / `device:001` convention with `hellologging:cache:products`, keeping keys greppable and deletable by prefix (a key-value store only does exact-key lookups).
- **Why it's fast** — in-memory, O(1) hash lookup where "the key is the index." Measured: MISS ≈ 526 ms (origin-bound) vs HIT ≈ 5 ms.
- **Trade-off acknowledged** — cache-aside accepts **bounded staleness** (≤ 60 s here), the eventual-consistency trade the course says is acceptable for read-heavy, non-critical data. Where staleness is unacceptable, the course's **write-through** pattern (write cache + backend together) is the alternative.

## Evidence

- `HelloLogging/Caching/RedisResponseCache.cs` — `ICacheStore` abstraction, `RedisCacheStore` (wraps `StringGetAsync`/`StringSetAsync` with expiry), `ResponseCache.GetOrCreateAsync<T>` (cache-aside, `System.Text.Json`), `CacheKeys.Products = "hellologging:cache:products"`, `ResponseCache.DefaultTtl = 60s`.
- `HelloLogging/Program.cs` — registers `ICacheStore → RedisCacheStore` (only when Redis is configured), maps `GET /products`, sets `X-Cache: HIT|MISS`, emits structured log `Cache {CacheStatus} for {CacheKey} in {ElapsedMs} ms`.
- `HelloLogging.Tests/Program.cs` — 3 new tests (miss calls origin + stores with exactly 60s TTL; hit skips origin; key is namespaced) using an in-memory `FakeCacheStore`, run by the existing console runner.
- No compose changes: `compose.yaml` already provides a healthchecked `redis:7-alpine`.

## PR-Style Summary

Implemented a Redis cache-aside layer with a 60-second TTL on a new `/products` endpoint of the `HelloLogging` API. Reads check Redis first and fall back to a slow origin only on a miss, then populate the cache with an atomic SET-with-expiry; responses carry an `X-Cache: HIT/MISS` header and a structured `CacheStatus` log line that keeps the cache decision queryable alongside the Week 1 correlation/trace fields. This applies the key-value course guidance — cache-aside + TTL + key namespacing — and accepts bounded (≤60s) staleness as the deliberate read-performance trade-off, noting write-through as the alternative when freshness is critical.

## Verification

```powershell
# Unit tests (no Redis needed)
cd demos
dotnet run --project .\HelloLogging.Tests\HelloLogging.Tests.csproj

# Live stack
docker compose up -d --build
docker compose ps                       # db + redis (healthy), api Up

curl.exe -s -i http://localhost:8080/products   # call 1
curl.exe -s -i http://localhost:8080/products   # call 2
docker compose exec redis redis-cli TTL hellologging:cache:products
docker compose logs api --no-log-prefix | Select-String '"CacheStatus"'
docker compose down
```

Observed results:

```text
# unit tests
PASS uses incoming correlation id
PASS creates correlation id when missing
PASS builds structured request log fields
PASS cache miss calls origin and stores with 60s ttl
PASS cache hit skips origin
PASS products cache key is namespaced

# call 1 — MISS
X-Cache: MISS
{"source":"origin","generatedAtUtc":"2026-07-15T17:05:10.2366131Z","items":[...]}   # ~741 ms

# call 2 — HIT (same generatedAtUtc, served from Redis)
X-Cache: HIT
{"source":"cache","generatedAtUtc":"2026-07-15T17:05:10.2366131Z","items":[...]}    # ~48 ms

# TTL counts down from 60
> TTL hellologging:cache:products
60

# structured log lines (CompactJson)
{"@t":"...","@mt":"Cache {CacheStatus} for {CacheKey} in {ElapsedMs} ms","CacheStatus":"MISS","CacheKey":"hellologging:cache:products","ElapsedMs":526,"CorrelationId":"...","TraceId":"...","RequestPath":"/products",...}
{"@t":"...","CacheStatus":"HIT","ElapsedMs":5,...}

# after the key expires (or redis-cli DEL): fresh MISS with a NEW timestamp
X-Cache: MISS
{"source":"origin","generatedAtUtc":"2026-07-15T17:05:25.8397858Z","items":[...]}
```
