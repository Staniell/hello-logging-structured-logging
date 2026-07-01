using HelloLogging.Observability;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter());
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

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

app.Run();
