namespace HelloLogging.Observability;

public sealed record RequestLogFields(
    string HttpMethod,
    string RequestPath,
    int StatusCode,
    string CorrelationId,
    long ElapsedMilliseconds)
{
    public static RequestLogFields From(HttpContext context, string correlationId, long elapsedMilliseconds)
    {
        return new RequestLogFields(
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.Response.StatusCode,
            correlationId,
            elapsedMilliseconds);
    }
}
