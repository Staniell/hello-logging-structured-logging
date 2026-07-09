# HelloLogging Training Deliverable

This repository contains the Week 1 training deliverables for ASP.NET Core observability and containerization.

The implementation demonstrates:

- Serilog JSON console logging.
- Structured message templates.
- Request start, completion, and error logging.
- Correlation IDs with `X-Correlation-ID`.
- Queryable request context fields such as method, path, status code, trace ID, span ID, and elapsed time.
- A multi-stage Dockerfile for the API (non-root runtime, port 8080).
- A Docker Compose stack: API + PostgreSQL + Redis with health-gated startup.

See `HelloLogging/README.md` and `outputs/` for per-day run steps and submission notes.

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
| `GET http://localhost:8080/fail` | Throws to demonstrate structured exception logging |

Quick smoke test:

```powershell
curl.exe -s http://localhost:8080/health   # Healthy
curl.exe -s http://localhost:8080/hits    # {"hits":1}, then 2, 3, ... on repeat calls
```

### Services

| Service | Image | Notes |
| --- | --- | --- |
| `api` | built from `HelloLogging/Dockerfile` | Config via `ConnectionStrings__Db` / `ConnectionStrings__Redis` env vars |
| `db` | `postgres:17-alpine` | Data persisted in the `pgdata` volume; healthcheck `pg_isready` |
| `redis` | `redis:7-alpine` | Healthcheck `redis-cli ping`; reused in Week 2 for caching |

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

## Run locally without containers

The DB and Redis wiring is optional — without connection strings nothing is registered and `/health` reports `Healthy`:

```powershell
dotnet run --project .\HelloLogging\HelloLogging.csproj --urls http://127.0.0.1:5123
```
