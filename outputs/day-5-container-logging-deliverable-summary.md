# Day 5 Container Logging Deliverable Summary

## What Was Built

The integration day: verified end-to-end that the Serilog structured logging from Day 1–2 works unchanged inside the Dockerized stack from Day 3–4, and made the container logs demo-clean. The repo is now demo-ready for the Week 2 review.

## Evidence

- `docker compose logs api` shows the same CompactJson entries as a local run — Serilog writes to stdout, which is exactly what container log collection expects.
- Correlation ID round trip works through the container boundary: a request sent with `X-Correlation-ID: docker-demo-001` returns that ID in the response header/body, and every log entry for the request carries `"CorrelationId":"docker-demo-001"` plus trace/span IDs.
- `GET /fail` in the container produces a structured error entry with `"@l":"Error"` and the full exception in `"@x"`, still tagged with the caller's correlation ID.
- Added a `Serilog.MinimumLevel.Override` (`Microsoft.AspNetCore` → `Warning`) so container output shows the app's request logs rather than framework noise (containers run as `Production`, where the default `Logging` section does not apply to Serilog).
- `/health` polling stays out of the request logs (middleware bypass), keeping `docker compose logs -f api` readable during a live demo.
- Captured samples in `outputs/sample-container-log-entry.md`.

## PR-Style Summary

Verified the structured-logging + Docker integration end to end. JSON logs with correlation IDs, trace context, and structured exception details now stream from `docker compose logs`, with Serilog minimum-level overrides keeping container output focused on application events. Together with the Day 3 Dockerfile and Day 4 compose stack, the repository is a complete demo: one command brings up API + PostgreSQL + Redis, and the logs prove request tracing works across the container boundary.

## Verification (demo script)

Run from the repository root:

```powershell
docker compose up -d --build
docker compose ps                                                   # db + redis (healthy), api Up

Invoke-WebRequest http://localhost:8080/ -Headers @{ "X-Correlation-ID" = "docker-demo-001" }
curl.exe -s http://localhost:8080/fail -H "X-Correlation-ID: docker-demo-002"
curl.exe -s http://localhost:8080/hits

docker compose logs api --no-log-prefix | Select-String 'docker-demo'
docker compose down
```

Observed results:

```text
- Response header X-Correlation-ID: docker-demo-001 (round trip confirmed)
- Log entries "HTTP request started/completed" with "CorrelationId":"docker-demo-001"
- /fail entry with "@l":"Error" and "@x":"System.InvalidOperationException: Sample failure..."
- /hits entry "Redis hit counter incremented to {HitCount}"
```
