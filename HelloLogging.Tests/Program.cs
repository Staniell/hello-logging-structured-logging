using HelloLogging.Observability;
using Microsoft.AspNetCore.Http;

var tests = new List<(string Name, Action Test)>
{
    ("uses incoming correlation id", UsesIncomingCorrelationId),
    ("creates correlation id when missing", CreatesCorrelationIdWhenMissing),
    ("builds structured request log fields", BuildsStructuredRequestLogFields)
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
