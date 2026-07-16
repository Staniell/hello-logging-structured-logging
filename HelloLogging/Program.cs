using System.Diagnostics;
using HelloLogging.Caching;
using HelloLogging.Observability;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter());
});

// DB and Redis are optional: without connection strings (plain `dotnet run`) the app
// registers no checks and /health still reports Healthy.
var dbConnectionString = builder.Configuration.GetConnectionString("Db");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

var healthChecks = builder.Services.AddHealthChecks();

if (!string.IsNullOrWhiteSpace(dbConnectionString))
{
    healthChecks.AddNpgSql(dbConnectionString, name: "postgres");
}

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var options = ConfigurationOptions.Parse(redisConnectionString);
        // Connect lazily and keep retrying instead of crashing startup if Redis is down.
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    });
    healthChecks.AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis");
    builder.Services.AddSingleton<ICacheStore, RedisCacheStore>();
}

var app = builder.Build();

// Health probes are polled by infrastructure; keep them out of the request logs.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseMiddleware<CorrelationIdMiddleware>());

app.MapGet("/", (ILogger<Program> logger, HttpContext context) =>
{
    const string whoToGreet = "training reviewer";
    logger.LogInformation("Hello, {WhoToGreet}", whoToGreet);

    return Results.Ok(new
    {
        Message = $"Hello, {whoToGreet}!",
        CorrelationId = context.Items[CorrelationIdMiddleware.ItemName]
    });
});

app.MapGet("/fail", () =>
{
    throw new InvalidOperationException("Sample failure for structured exception logging.");
});

app.MapHealthChecks("/health");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    app.MapGet("/hits", async (IConnectionMultiplexer redis, ILogger<Program> logger) =>
    {
        var hits = await redis.GetDatabase().StringIncrementAsync("hellologging:hits");
        logger.LogInformation("Redis hit counter incremented to {HitCount}", hits);

        return Results.Ok(new { Hits = hits });
    });

    app.MapGet("/products", async (ICacheStore cache, ILogger<Program> logger, HttpContext context) =>
    {
        var stopwatch = Stopwatch.StartNew();

        var (products, fromCache) = await ResponseCache.GetOrCreateAsync(
            cache,
            CacheKeys.Products,
            ResponseCache.DefaultTtl,
            async () =>
            {
                // Stand-in for a slow upstream call so cache hits are visibly faster.
                await Task.Delay(500);
                return new ProductsResponse(
                    [
                        new Product("P1001", "Wireless Headphones", 129.99m),
                        new Product("P1002", "Gaming Mouse", 59.99m),
                        new Product("P1003", "Mechanical Keyboard", 89.99m)
                    ],
                    DateTime.UtcNow);
            });

        var cacheStatus = fromCache ? "HIT" : "MISS";
        context.Response.Headers["X-Cache"] = cacheStatus;
        logger.LogInformation(
            "Cache {CacheStatus} for {CacheKey} in {ElapsedMs} ms",
            cacheStatus, CacheKeys.Products, stopwatch.ElapsedMilliseconds);

        return Results.Ok(new
        {
            Source = fromCache ? "cache" : "origin",
            products.GeneratedAtUtc,
            products.Items
        });
    });
}

app.Run();

public sealed record Product(string Id, string Name, decimal Price);

public sealed record ProductsResponse(List<Product> Items, DateTime GeneratedAtUtc);
