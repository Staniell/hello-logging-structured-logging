using System.Diagnostics;

namespace HelloLogging.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var startedAt = Stopwatch.StartNew();
        using var scope = _logger.BeginScope(CreateScope(context, correlationId));

        var startFields = RequestLogFields.From(context, correlationId, 0);
        _logger.LogInformation(
            "HTTP request started {HttpMethod} {RequestPath}",
            startFields.HttpMethod,
            startFields.RequestPath);

        try
        {
            await _next(context);

            startedAt.Stop();
            var completedFields = RequestLogFields.From(context, correlationId, startedAt.ElapsedMilliseconds);
            _logger.LogInformation(
                "HTTP request completed {HttpMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms",
                completedFields.HttpMethod,
                completedFields.RequestPath,
                completedFields.StatusCode,
                completedFields.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            startedAt.Stop();
            var failedFields = RequestLogFields.From(context, correlationId, startedAt.ElapsedMilliseconds);
            _logger.LogError(
                ex,
                "HTTP request failed {HttpMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms",
                failedFields.HttpMethod,
                failedFields.RequestPath,
                failedFields.StatusCode,
                failedFields.ElapsedMilliseconds);

            throw;
        }
    }

    public static string GetOrCreateCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming;

        context.Items[ItemName] = correlationId;
        return correlationId;
    }

    private static Dictionary<string, object?> CreateScope(HttpContext context, string correlationId)
    {
        var activity = Activity.Current;

        return new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = activity?.TraceId.ToString(),
            ["SpanId"] = activity?.SpanId.ToString(),
            ["RequestId"] = context.TraceIdentifier,
            ["RequestPath"] = context.Request.Path.Value,
            ["HttpMethod"] = context.Request.Method
        };
    }
}
