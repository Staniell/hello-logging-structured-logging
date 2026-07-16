# W2 Thursday — Prometheus + Grafana Monitoring Deliverable Summary

## What Was Built

Extended the **W1 Thursday Docker Compose deliverable** (`compose.yaml`, see `day-4-compose-deliverable-summary.md`) with two new health-gated services: **Prometheus** (`prom/prometheus:v3.13.0`) and **Grafana** (`grafana/grafana:13.0.3`). The API now exposes a `/metrics` endpoint via `prometheus-net.AspNetCore` on the existing port 8080; Prometheus scrapes it every 5 seconds over the compose network (`api:8080`, the same service-name wiring as `db`/`redis` from Week 1). Grafana is **fully provisioned as code** — datasource, a dashboard showing request rate by endpoint and 5xx error count/rate, and one alert rule (**High 5xx error rate**, fires when the 5xx rate exceeds 0.05 req/s) all load from files in `monitoring/`, with zero manual UI configuration.

One correctness subtlety surfaced during implementation: prometheus-net records the status code in a `finally` block *after* the pipeline unwinds, and this app deliberately has no exception handler — so `/fail`'s unhandled exception would have been counted as a `200` and the 5xx alert could never fire. A new `UnhandledExceptionStatusMiddleware` stamps such requests as `500` before rethrowing, which makes both the metrics and the Serilog "HTTP request failed" log line (previously also reporting 200) truthful.

## Course concepts applied (*Docker Monitoring with Prometheus and Grafana*, *Grafana Concepts and Basic Configuration*)

- **Pull-based scraping model** — Prometheus pulls `/metrics` from the API on an interval; the app never pushes, it only exposes a text endpoint.
- **Counters + PromQL `rate()`** — `http_requests_received_total` only ever increases; per-second rates are derived at query time (`rate(...[1m])`), which survives restarts and missed scrapes.
- **Label-based filtering** — the metric's `code` and `endpoint` labels drive everything: the dashboard splits traffic by `endpoint`, filters out infrastructure noise (`endpoint!~"/metrics|/health"`), and the alert selects errors with `code=~"5.."`.
- **Provisioning as code** — datasources, dashboard providers, dashboards, and alert rules load from YAML/JSON on disk (`monitoring/grafana/provisioning/`), the reproducible alternative to click-configuration.
- **Unified alerting anatomy** — the rule is a query (A: instant PromQL) plus a threshold expression condition (C: `A > 0.05`), an evaluation interval (10s), a pending period (`for: 0s`, demo speed), and explicit no-data handling (`noDataState: OK`, because the 5xx series doesn't exist until the first error).
- **Dashboards mirror alerts** — the 5xx panel draws the alert threshold as a red line at 0.05, so the dashboard and the alert tell the same story.

## Evidence

- `compose.yaml` — `prometheus` and `grafana` services added to the W1 Thursday stack, with the same 5s/3s/12-retry healthcheck pattern; Grafana is health-gated on Prometheus. Config is bind-mounted read-only; state lives in new named volumes `prometheus-data` and `grafana-data`.
- `monitoring/prometheus/prometheus.yml` — 5s scrape of `api:8080/metrics` plus a self-scrape job.
- `monitoring/grafana/provisioning/datasources/prometheus.yml` — Prometheus datasource with fixed `uid: prometheus` so dashboard and alert references are deterministic.
- `monitoring/grafana/provisioning/dashboards/dashboards.yml` + `monitoring/grafana/dashboards/hellologging-monitoring.json` — hand-written minimal dashboard (`uid: hellologging-monitoring`): request rate by endpoint, 5xx count (15m), 5xx rate with the alert threshold drawn at 0.05 req/s.
- `monitoring/grafana/provisioning/alerting/hellologging-alerts.yml` — alert rule `High 5xx error rate`: `sum(rate(http_requests_received_total{code=~"5.."}[1m]))` with threshold condition `A > 0.05`.
- `HelloLogging/HelloLogging.csproj` — added `prometheus-net.AspNetCore` 8.2.1.
- `HelloLogging/Program.cs` — `app.UseHttpMetrics()` (counts every request; the dashboard filters at query time), `/metrics` added to the request-log exclusion so 5s scrapes don't flood the Serilog output, `app.MapMetrics()`.
- `HelloLogging/Observability/UnhandledExceptionStatusMiddleware.cs` — stamps unhandled exceptions as 500 for metrics and logs (see What Was Built).
- `HelloLogging.Tests/Program.cs` — new test: `unhandled exception is stamped as 500 for metrics`.
- `scripts/generate-traffic.ps1` — demo traffic generator (`/`, `/products`, `/hits`, plus `/fail` every 5th loop) to light up the dashboard and fire the alert.

## PR-Style Summary

Added end-to-end monitoring to the Dockerized HelloLogging stack. The API exposes Prometheus metrics through prometheus-net; Prometheus and Grafana containers join the Week 1 compose stack behind the existing health-gated startup pattern; and Grafana arrives fully configured from provisioning files — datasource, a requests/errors dashboard, and a 5xx-rate alert with a 0.05 req/s threshold. A small middleware stamps unhandled exceptions as 500 so error metrics (and the existing failure logs) report the real status code. `docker compose up -d --build` plus the traffic script reproduces the full demo — live dashboard and firing alert — on any machine with Docker Desktop.

## Verification

Run:

```powershell
dotnet run --project .\HelloLogging.Tests\HelloLogging.Tests.csproj
docker compose up -d --build
docker compose ps
curl.exe -s http://localhost:8080/metrics | Select-String "http_requests_received_total" | Select-Object -First 2
curl.exe -s http://localhost:9090/api/v1/targets   # both jobs "health":"up"
powershell -File .\scripts\generate-traffic.ps1    # ~3 minutes of traffic + errors
curl.exe -s -u admin:grafana-local http://localhost:3000/api/datasources/uid/prometheus/health
curl.exe -s -u admin:grafana-local "http://localhost:3000/api/prometheus/grafana/api/v1/rules"
```

Observed results:

```text
PASS unhandled exception is stamped as 500 for metrics   (7/7 tests pass)

docker compose ps: db, redis, prometheus, grafana => (healthy); api => Up

# HELP http_requests_received_total Provides the count of HTTP requests that have
# been processed by the ASP.NET Core pipeline.

Prometheus targets: hellologging-api => up, prometheus => up

Grafana datasource health: {"message":"Successfully queried the Prometheus API.","status":"OK"}

Under traffic (rate over the last 1m):
  endpoint /:         1.8   req/s
  endpoint /products: 1.8   req/s
  endpoint /hits:     1.8   req/s
  endpoint /fail:     0.364 req/s
  5xx rate: 0.345 req/s  => above the 0.05 threshold

Alert rule: High 5xx error rate | state: firing | health: ok

Container log for GET /fail now records the true status:
  "@mt":"HTTP request failed ... with {StatusCode}", "StatusCode":500
```

Screenshots (dashboard with live metrics; alert rule with its threshold condition, state Firing) are captured in the training deliverables folder `W2/D4/`.
