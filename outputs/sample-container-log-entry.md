# Sample Container Log Entry

Verified log line captured from `docker compose logs api` (same CompactJson shape as a local run, now flowing through container stdout):

```json
{
  "@t": "2026-07-09T21:41:12.9378158Z",
  "@mt": "HTTP request completed {HttpMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms",
  "@tr": "fa65ff2b3121e4be7e334deaca76ea19",
  "@sp": "a9c84d6c5e736799",
  "HttpMethod": "GET",
  "RequestPath": "/",
  "StatusCode": 200,
  "ElapsedMilliseconds": 7,
  "SourceContext": "HelloLogging.Observability.CorrelationIdMiddleware",
  "CorrelationId": "docker-demo-001",
  "TraceId": "fa65ff2b3121e4be7e334deaca76ea19",
  "SpanId": "a9c84d6c5e736799",
  "RequestId": "0HNMU0A85O7E4:00000001",
  "ConnectionId": "0HNMU0A85O7E4"
}
```

And a structured error entry from `GET /fail` inside the container — `@l` carries the level and `@x` the full stack trace, still tagged with the caller's correlation ID:

```json
{
  "@t": "2026-07-09T21:41:12.9900917Z",
  "@mt": "HTTP request failed {HttpMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms",
  "@l": "Error",
  "@x": "System.InvalidOperationException: Sample failure for structured exception logging.\n   at Program.<>c.<<Main>$>b__0_6() in /src/HelloLogging/Program.cs:line 61\n   ...",
  "HttpMethod": "GET",
  "RequestPath": "/fail",
  "SourceContext": "HelloLogging.Observability.CorrelationIdMiddleware",
  "CorrelationId": "docker-demo-002",
  "TraceId": "88a984931aaa89a6843bcbb22cc300e6"
}
```

Because containers write logs to stdout, any log collector (Docker log driver, Loki, ELK, CloudWatch) receives these as parseable JSON — nothing about the logging pipeline changed between `dotnet run` and Docker.
