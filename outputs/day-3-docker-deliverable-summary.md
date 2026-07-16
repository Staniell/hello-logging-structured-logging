# Day 3 Docker Deliverable Summary

## What Was Built

Containerized the `HelloLogging` API with a multi-stage Dockerfile (`HelloLogging/Dockerfile`) plus a root `.dockerignore`. The image builds and runs cleanly with no code changes to the app.

## Evidence

- Build stage uses `mcr.microsoft.com/dotnet/sdk:10.0`; runtime stage uses `mcr.microsoft.com/dotnet/aspnet:10.0`.
- `NuGet.config` and the `.csproj` are copied before `dotnet restore`, so source-only edits reuse the cached restore layer.
- `dotnet publish -c Release --no-restore` output is the only content copied into the runtime image.
- The container runs as the non-root `app` user (`docker exec ... whoami` returns `app`) and listens on the image default port 8080 (`ASPNETCORE_HTTP_PORTS=8080`).
- `.dockerignore` excludes `bin/`, `obj/`, git internals, and docs so stale Windows build artifacts never leak into the Linux image.
- Build context is the repository root (`docker build -f HelloLogging/Dockerfile .`) because `NuGet.config` lives there and a build context cannot reach parent directories.

## PR-Style Summary

Added a multi-stage Dockerfile for the `HelloLogging` API. The build stage restores with the repo's pinned NuGet source and publishes a Release build; the runtime stage is the ASP.NET Core 10 base image running the published output as a non-root user on port 8080. A root `.dockerignore` keeps the build context minimal. Serilog JSON logs flow to stdout, so `docker logs` shows the same structured output as a local run.

## Verification

Run from the repository root:

```powershell
docker build -f HelloLogging/Dockerfile -t hellologging-api:local .
docker run --rm -d -p 8080:8080 --name hello-wed hellologging-api:local
curl.exe -s -i http://localhost:8080/ -H "X-Correlation-ID: wed-demo-001"
docker exec hello-wed whoami
docker logs hello-wed
docker stop hello-wed
```

Observed results:

```text
HTTP/1.1 200 OK
X-Correlation-ID: wed-demo-001

{"message":"Hello, training reviewer!","correlationId":"wed-demo-001"}
```

`whoami` returned `app`, and `docker logs` showed CompactJson structured entries, e.g.:

```json
{"@t":"2026-07-09T21:36:12.2951702Z","@mt":"HTTP request completed {HttpMethod} {RequestPath} with {StatusCode} in {ElapsedMilliseconds} ms","HttpMethod":"GET","RequestPath":"/","StatusCode":200,"ElapsedMilliseconds":27,"SourceContext":"HelloLogging.Observability.CorrelationIdMiddleware","CorrelationId":"wed-demo-001"}
```
