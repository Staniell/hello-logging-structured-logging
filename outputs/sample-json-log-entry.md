# Sample JSON Log Entry

Verified request completion log shape produced by the app:

```json
{
  "@t": "2026-07-01T11:13:59.1090467Z",
  "@mt": "HTTP request completed {HttpMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms",
  "@tr": "f41d40226f9b7f0d3a9c1d3a7e62852f",
  "@sp": "723cc2a5f7bfc774",
  "HttpMethod": "GET",
  "RequestPath": "/",
  "StatusCode": 200,
  "ElapsedMilliseconds": 48,
  "SourceContext": "HelloLogging.Observability.CorrelationIdMiddleware",
  "CorrelationId": "training-demo-001",
  "TraceId": "f41d40226f9b7f0d3a9c1d3a7e62852f",
  "SpanId": "723cc2a5f7bfc774",
  "RequestId": "0HNMNC7CK679E:00000001",
  "ConnectionId": "0HNMNC7CK679E"
}
```

The important part is that values such as `HttpMethod`, `RequestPath`, `StatusCode`, `ElapsedMilliseconds`, and `CorrelationId` are fields. They can be filtered, grouped, searched, and correlated instead of being trapped inside one plain text message.
