namespace HelloLogging.Observability;

// prometheus-net counts a request in a finally block by reading Response.StatusCode.
// Without an exception handler in the pipeline, an unhandled exception (e.g. GET /fail)
// unwinds while the status code is still 200, so the request would be recorded as a
// success and the 5xx alert could never fire. Stamp 500 and rethrow: metrics and the
// request-failed log line see the real status, while Kestrel's response is unchanged.
public sealed class UnhandledExceptionStatusMiddleware
{
    private readonly RequestDelegate _next;

    public UnhandledExceptionStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }

            throw;
        }
    }
}
