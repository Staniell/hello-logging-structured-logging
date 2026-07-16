using HelloLogging.Caching;
using HelloLogging.Observability;
using Microsoft.AspNetCore.Http;

var tests = new List<(string Name, Action Test)>
{
    ("uses incoming correlation id", UsesIncomingCorrelationId),
    ("creates correlation id when missing", CreatesCorrelationIdWhenMissing),
    ("builds structured request log fields", BuildsStructuredRequestLogFields),
    ("cache miss calls origin and stores with 60s ttl", CacheMissCallsOriginAndStoresWithTtl),
    ("cache hit skips origin", CacheHitSkipsOrigin),
    ("products cache key is namespaced", ProductsCacheKeyIsNamespaced),
    ("unhandled exception is stamped as 500 for metrics", UnhandledExceptionStampsFiveHundred)
};

var failures = new List<string>();

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex);
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failed tests:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

static void UsesIncomingCorrelationId()
{
    var context = new DefaultHttpContext();
    context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "training-run-123";

    var correlationId = CorrelationIdMiddleware.GetOrCreateCorrelationId(context);

    AssertEqual("training-run-123", correlationId);
    AssertEqual("training-run-123", context.Items[CorrelationIdMiddleware.ItemName]);
}

static void CreatesCorrelationIdWhenMissing()
{
    var context = new DefaultHttpContext();

    var correlationId = CorrelationIdMiddleware.GetOrCreateCorrelationId(context);

    AssertTrue(!string.IsNullOrWhiteSpace(correlationId), "Correlation ID should be generated.");
    AssertEqual(correlationId, context.Items[CorrelationIdMiddleware.ItemName]);
}

static void BuildsStructuredRequestLogFields()
{
    var context = new DefaultHttpContext();
    context.Request.Method = "POST";
    context.Request.Path = "/api/products/42";
    context.Response.StatusCode = StatusCodes.Status202Accepted;

    var fields = RequestLogFields.From(context, "checkout-8a74d3", 37);

    AssertEqual("POST", fields.HttpMethod);
    AssertEqual("/api/products/42", fields.RequestPath);
    AssertEqual(202, fields.StatusCode);
    AssertEqual("checkout-8a74d3", fields.CorrelationId);
    AssertEqual(37L, fields.ElapsedMilliseconds);
}

static void CacheMissCallsOriginAndStoresWithTtl()
{
    var store = new FakeCacheStore();
    var originCalls = 0;

    var (value, fromCache) = ResponseCache.GetOrCreateAsync(
            store,
            CacheKeys.Products,
            ResponseCache.DefaultTtl,
            () =>
            {
                originCalls++;
                return Task.FromResult("fresh-products");
            })
        .GetAwaiter().GetResult();

    AssertEqual("fresh-products", value);
    AssertEqual(false, fromCache);
    AssertEqual(1, originCalls);
    AssertEqual(TimeSpan.FromSeconds(60), store.LastTtl ?? TimeSpan.Zero);
    AssertTrue(store.Values.ContainsKey(CacheKeys.Products), "Origin value should be stored under the cache key.");
}

static void CacheHitSkipsOrigin()
{
    var store = new FakeCacheStore();
    _ = ResponseCache.GetOrCreateAsync(
            store,
            CacheKeys.Products,
            ResponseCache.DefaultTtl,
            () => Task.FromResult("cached-products"))
        .GetAwaiter().GetResult();

    var originCalls = 0;
    var (value, fromCache) = ResponseCache.GetOrCreateAsync(
            store,
            CacheKeys.Products,
            ResponseCache.DefaultTtl,
            () =>
            {
                originCalls++;
                return Task.FromResult("should-not-be-used");
            })
        .GetAwaiter().GetResult();

    AssertEqual("cached-products", value);
    AssertEqual(true, fromCache);
    AssertEqual(0, originCalls);
}

static void ProductsCacheKeyIsNamespaced()
{
    AssertEqual("hellologging:cache:products", CacheKeys.Products);
    AssertTrue(CacheKeys.Products.StartsWith("hellologging:"), "Cache keys should be namespaced by app.");
}

static void UnhandledExceptionStampsFiveHundred()
{
    var context = new DefaultHttpContext();
    var middleware = new UnhandledExceptionStatusMiddleware(
        _ => throw new InvalidOperationException("Sample failure."));

    var rethrown = false;
    try
    {
        middleware.InvokeAsync(context).GetAwaiter().GetResult();
    }
    catch (InvalidOperationException)
    {
        rethrown = true;
    }

    AssertTrue(rethrown, "The original exception should propagate past the middleware.");
    AssertEqual(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
}

static void AssertEqual<T>(T expected, T? actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FakeCacheStore : ICacheStore
{
    public Dictionary<string, string> Values { get; } = new();

    public TimeSpan? LastTtl { get; private set; }

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value, TimeSpan ttl)
    {
        Values[key] = value;
        LastTtl = ttl;
        return Task.CompletedTask;
    }
}
