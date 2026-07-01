# Day 1 Logging Deliverable Summary

## What Was Built

Built a small ASP.NET Core 10 API in `HelloLogging` that demonstrates structured logging for request observability.

## Evidence

- Serilog is installed through `Serilog.AspNetCore`.
- Logs are emitted as JSON using `CompactJsonFormatter`.
- Request logging middleware records request start, completion, and failure.
- Each request has a correlation ID from `X-Correlation-ID` or a generated value.
- Request context is added as structured fields through a logging scope.
- A sample endpoint logs `Hello, {WhoToGreet}` to demonstrate message-template logging.
- A `/fail` endpoint demonstrates structured exception logging.
- A test runner verifies correlation ID handling and request log field creation.

## PR-Style Summary

Implemented structured request logging for the `HelloLogging` sample API. The app now uses Serilog JSON console output, captures correlation IDs, and logs request start/end/error events with structured fields instead of interpolated strings. This applies the course guidance that logs should keep important values queryable for support, dashboards, and trace correlation.

## Verification

Run:

```powershell
dotnet run --project .\HelloLogging.Tests\HelloLogging.Tests.csproj
```

Expected result:

```text
PASS uses incoming correlation id
PASS creates correlation id when missing
PASS builds structured request log fields
```
