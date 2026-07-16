# HelloLogging Structured Logging Deliverable

This sample API demonstrates the Day 1 logging deliverable from the training plan:

- Serilog configured as the ASP.NET Core logging provider.
- JSON console logs through `CompactJsonFormatter`.
- Structured message templates instead of string interpolation.
- Request start, request completion, and exception logs.
- Correlation ID support through the `X-Correlation-ID` header.
- Request context added through logging scopes so fields are queryable.

## Run

```powershell
dotnet run --project .\HelloLogging\HelloLogging.csproj --urls http://127.0.0.1:5123
```

In another terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5123/ -Headers @{ "X-Correlation-ID" = "training-demo-001" }
```

To see exception logging:

```powershell
Invoke-WebRequest http://127.0.0.1:5123/fail -SkipHttpErrorCheck
```

## Run in Docker

Build from the repository root so `NuGet.config` is inside the build context:

```powershell
docker build -f HelloLogging/Dockerfile -t hellologging-api:local .
docker run --rm -d -p 8080:8080 --name hellologging hellologging-api:local
```

Then call the API on the mapped port:

```powershell
Invoke-RestMethod http://localhost:8080/ -Headers @{ "X-Correlation-ID" = "training-demo-001" }
docker logs hellologging
docker stop hellologging
```

The image is a multi-stage build: `dotnet/sdk:10.0` restores and publishes, `dotnet/aspnet:10.0` runs the published output as the non-root `app` user on port 8080.

### Configuration

The container reads optional connection strings from environment variables:

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__Db` | PostgreSQL health check |
| `ConnectionStrings__Redis` | Redis connection (health check + `/hits` counter) |

When a variable is absent the related wiring is skipped, so the app still runs standalone with `dotnet run` or a bare `docker run`.

## Test

```powershell
dotnet run --project .\HelloLogging.Tests\HelloLogging.Tests.csproj
```

## Course Takeaways Applied

The course showed that string logs and structured logs can render similarly, but structured logs keep named values as fields. The app uses templates such as:

```csharp
logger.LogInformation("Hello, {WhoToGreet}", whoToGreet);
```

That keeps `WhoToGreet` as data instead of flattening it into a final string. The request middleware applies the same pattern to HTTP method, path, status code, elapsed time, request ID, trace ID, span ID, and correlation ID.
