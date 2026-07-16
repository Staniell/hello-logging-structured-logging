# HelloLogging Training Deliverable

This repository contains the Week 1 and Week 2 training deliverables for ASP.NET Core observability, containerization, caching, and monitoring.

The implementation demonstrates:

- Serilog JSON console logging.
- Structured message templates.
- Request start, completion, and error logging.
- Correlation IDs with `X-Correlation-ID`.
- Queryable request context fields such as method, path, status code, trace ID, span ID, and elapsed time.
- A multi-stage Dockerfile for the API (non-root runtime, port 8080).
- A Docker Compose stack: API + PostgreSQL + Redis with health-gated startup.
- A Redis cache-aside layer with a 60-second TTL on `/products` (W2 Wednesday).
- Prometheus metrics on `/metrics` plus a fully provisioned Grafana dashboard and 5xx alert rule (W2 Thursday).

See `HelloLogging/README.md` and `outputs/` for per-day run steps and submission notes.

## Build and run the Docker image (W1 Wednesday)

Prerequisite: Docker Desktop running (`docker info` should print server details, not an error).

**1. Build — always from the repository root**, not from inside `HelloLogging/`. The build context must be the root so `NuGet.config` is visible to the restore step (a Docker build cannot reach files outside its context):

```powershell
docker build -f HelloLogging/Dockerfile -t hellologging-api:local .
```

The final line should read `naming to docker.io/library/hellologging-api:local`. The build is multi-stage: the `sdk:10.0` stage restores and publishes, and only the published output is copied into the `aspnet:10.0` runtime stage — so the final image contains no SDK, no source code, and no NuGet cache.

**2. Run** — map host port 8080 to the container's 8080 (the .NET runtime image listens there via `ASPNETCORE_HTTP_PORTS=8080`):

```powershell
docker run --rm -d -p 8080:8080 --name hellologging hellologging-api:local
```

`--rm` removes the container when it stops; `-d` runs it detached and prints the container ID.

**3. Verify it runs cleanly:**

```powershell
curl.exe -s -i http://localhost:8080/ -H "X-Correlation-ID: wed-demo-001"
# HTTP/1.1 200 OK, X-Correlation-ID echoed back, body:
# {"message":"Hello, training reviewer!","correlationId":"wed-demo-001"}

docker logs hellologging      # Serilog CompactJson lines: {"@t":...,"@mt":"HTTP request started ...
docker exec hellologging whoami   # app  (container runs as the non-root user)
```

**4. Stop** (the `--rm` flag also removes it):

```powershell
docker stop hellologging
```

Troubleshooting:

- `error during connect ... dockerDesktopLinuxEngine` — Docker Desktop isn't running; start it and retry.
- `Bind for 0.0.0.0:8080 failed: port is already allocated` — something else (e.g. the compose stack) holds port 8080; run `docker ps` and stop it, or map a different host port (`-p 8081:8080`).
- Stale build behaving oddly — force a fresh build with `docker build --no-cache ...`.

## Run the full stack (Docker Compose)

Prerequisite: Docker Desktop running.

```powershell
docker compose up -d --build
docker compose ps
```

`docker compose ps` should show `db` and `redis` as `(healthy)` and `api` as `Up`. The API only starts after both dependencies report healthy (`depends_on` with `condition: service_healthy`).

| Endpoint | Purpose |
| --- | --- |
| `GET http://localhost:8080/` | Hello response + correlation ID round trip |
| `GET http://localhost:8080/health` | Health checks for PostgreSQL and Redis (`Healthy` / `Unhealthy`) |
| `GET http://localhost:8080/hits` | Increments a counter in Redis and returns it |
| `GET http://localhost:8080/products` | W2 Wednesday: cache-aside Redis cache of a slow origin, 60s TTL (`X-Cache: HIT/MISS`) |
| `GET http://localhost:8080/fail` | Throws to demonstrate structured exception logging |
| `GET http://localhost:8080/metrics` | W2 Thursday: Prometheus metrics from `prometheus-net` (scraped by the `prometheus` container) |

Quick smoke test:

```powershell
curl.exe -s http://localhost:8080/health   # Healthy
curl.exe -s http://localhost:8080/hits    # {"hits":1}, then 2, 3, ... on repeat calls
curl.exe -s -i http://localhost:8080/products   # 1st call: X-Cache: MISS, ~500ms; repeats: HIT until the 60s TTL expires
```

### Services

| Service | Image | Notes |
| --- | --- | --- |
| `api` | built from `HelloLogging/Dockerfile` | Config via `ConnectionStrings__Db` / `ConnectionStrings__Redis` env vars |
| `db` | `postgres:17-alpine` | Data persisted in the `pgdata` volume; healthcheck `pg_isready` |
| `redis` | `redis:7-alpine` | Healthcheck `redis-cli ping`; reused in Week 2 for caching |
| `prometheus` | `prom/prometheus:v3.13.0` | Scrapes `api:8080/metrics` every 5s; config bind-mounted from `monitoring/prometheus/` |
| `grafana` | `grafana/grafana:13.0.3` | Datasource, dashboard, and alert rule provisioned from `monitoring/grafana/`; login `admin` / `grafana-local` |

### View container logs

```powershell
docker compose logs -f api
```

Log lines are Serilog CompactJson — the same structured output as a local run.

### Tear down

```powershell
docker compose down        # keep the pgdata volume (and the Redis-persisted state)
docker compose down -v     # also remove volumes for a clean slate
```

## Monitoring: Prometheus + Grafana (W2 Thursday)

This extends the **W1 Thursday Docker Compose stack** — the same `compose.yaml`, now with two more health-gated services (`prometheus`, `grafana`) alongside `api`, `db`, and `redis`. Nothing is configured by hand in the Grafana UI: the datasource, the dashboard, and the alert rule are all provisioned from files in `monitoring/`, so `docker compose up -d --build` gives a fully working monitoring stack on any machine.

```
monitoring/
├── prometheus/prometheus.yml                        # scrapes api:8080/metrics every 5s (+ self-scrape)
└── grafana/
    ├── provisioning/
    │   ├── datasources/prometheus.yml               # Prometheus datasource, fixed uid "prometheus"
    │   ├── dashboards/dashboards.yml                # file provider for the dashboards/ folder
    │   └── alerting/hellologging-alerts.yml         # alert rule: High 5xx error rate (> 0.05 req/s)
    └── dashboards/hellologging-monitoring.json      # request rate by endpoint + 5xx panels
```

The API side is `prometheus-net.AspNetCore`: `app.UseHttpMetrics()` + `app.MapMetrics()` expose `http_requests_received_total` (and friends) on the existing port 8080 — no new listener, no Dockerfile change. One subtlety worth knowing: prometheus-net records the status code *after* the pipeline unwinds, and this app deliberately has no exception handler, so an unhandled exception would be counted as a `200`. `UnhandledExceptionStatusMiddleware` stamps such requests as `500` before rethrowing, which is what makes the 5xx panels and the alert truthful.

### Demo

```powershell
docker compose up -d --build
docker compose ps                                 # prometheus + grafana must be (healthy)

# Generate ~3 minutes of traffic including errors (every 5th loop hits /fail):
powershell -File .\scripts\generate-traffic.ps1
```

Then open:

| URL | What you see |
| --- | --- |
| `http://localhost:3000/d/hellologging-monitoring` | Dashboard **HelloLogging — Requests & Errors** (login `admin` / `grafana-local`) |
| `http://localhost:3000/alerting/list` | Alert rule **High 5xx error rate** — fires when 5xx rate exceeds 0.05 req/s for one 10s evaluation |
| `http://localhost:9090/targets` | Prometheus scrape targets (`hellologging-api` must be UP) |

The dashboard panels filter out `/metrics` and `/health` traffic at query time (`endpoint!~"/metrics|/health"`), so what you see is real app traffic. The request logs do the same: scrapes and health polls are excluded from the Serilog request logging, keeping container output demo-clean.

Windows/Docker Desktop notes: only *config* is bind-mounted (relative `./monitoring/...` paths); Prometheus metric history and Grafana state live in named volumes (`prometheus-data`, `grafana-data`) because SQLite on NTFS bind mounts is unreliable. `docker compose down` keeps both volumes, so dashboards retain history across restarts; `docker compose down -v` wipes them.

## Week 2 review demo script

Five minutes, one terminal, from the repository root:

```powershell
# 1. One command brings up API + PostgreSQL + Redis, health-gated
docker compose up -d --build
docker compose ps

# 2. Correlation ID round trip through the container
Invoke-WebRequest http://localhost:8080/ -Headers @{ "X-Correlation-ID" = "demo-review-001" }

# 3. Redis is genuinely wired: counter survives repeated calls
curl.exe -s http://localhost:8080/hits
curl.exe -s http://localhost:8080/hits

# 4. Structured error logging with stack trace
curl.exe -s http://localhost:8080/fail

# 5. The proof: JSON logs with correlation IDs in container output
docker compose logs api --no-log-prefix | Select-String 'demo-review-001'

# 6. Dependency outage shows up in health, and recovers
docker compose stop redis
curl.exe -s http://localhost:8080/health     # Unhealthy (503)
docker compose start redis
curl.exe -s http://localhost:8080/health     # Healthy (200)

docker compose down
```

Sample captured output lives in `outputs/sample-container-log-entry.md`.

## Run locally without containers

The DB and Redis wiring is optional — without connection strings nothing is registered and `/health` reports `Healthy`:

```powershell
dotnet run --project .\HelloLogging\HelloLogging.csproj --urls http://127.0.0.1:5123
```
