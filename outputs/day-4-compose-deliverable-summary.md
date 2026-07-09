# Day 4 Compose Deliverable Summary

## What Was Built

A Docker Compose stack (`compose.yaml`) that runs three containers together — the `HelloLogging` API, PostgreSQL 17, and Redis 7 — plus real wiring in the app: health checks for both dependencies and a Redis-backed `/hits` counter that Week 2 caching will build on.

## Evidence

- `compose.yaml` defines `api` (built from `HelloLogging/Dockerfile`), `db` (`postgres:17-alpine`, `pgdata` volume), and `redis` (`redis:7-alpine`).
- `db` and `redis` have container healthchecks (`pg_isready -U hellologging -d hellologging`, `redis-cli ping`); `api` uses `depends_on` with `condition: service_healthy`, so it starts only after both report healthy.
- Connection strings reach the API as environment variables (`ConnectionStrings__Db`, `ConnectionStrings__Redis`) — configuration, not code.
- `Program.cs` registers an Npgsql health check, a Redis health check, and a shared `IConnectionMultiplexer` (with `AbortOnConnectFail = false` so a Redis outage degrades health instead of crashing startup). All of it is conditional on the connection strings, so plain `dotnet run` without containers still works.
- `GET /hits` performs `StringIncrementAsync` against Redis — a genuine round trip, verified from inside the Redis container.
- `/health` requests bypass the correlation-ID request logging middleware so infrastructure polling does not flood the logs.

## PR-Style Summary

Added a Docker Compose stack running the API with PostgreSQL and Redis. Startup is health-gated: the API waits for both dependencies to pass their container healthchecks. The app now exposes `/health` (Npgsql + Redis checks) and `/hits` (Redis counter) when connection strings are supplied via environment variables, while remaining fully runnable standalone. Redis is wired through a DI-registered `IConnectionMultiplexer`, ready to back the Week 2 caching deliverable.

## Verification

Run from the repository root:

```powershell
docker compose up -d --build
docker compose ps
curl.exe -s http://localhost:8080/health
curl.exe -s http://localhost:8080/hits
curl.exe -s http://localhost:8080/hits
docker compose exec redis redis-cli GET hellologging:hits
docker compose exec db psql -U hellologging -d hellologging -c "SELECT 1;"
```

Observed results:

```text
NAME                   IMAGE                    STATUS
hellologging-api-1     hellologging-api:local   Up
hellologging-db-1      postgres:17-alpine       Up (healthy)
hellologging-redis-1   redis:7-alpine           Up (healthy)

health: Healthy
hits:   {"hits":1} then {"hits":2}
redis:  GET hellologging:hits -> "2"
psql:   SELECT 1 -> 1 row
```

Failure drill — dependency outage surfaces in `/health` and recovers without restarting the API:

```text
docker compose stop redis   ->  GET /health = 503 Unhealthy
docker compose start redis  ->  GET /health = 200 Healthy
```
