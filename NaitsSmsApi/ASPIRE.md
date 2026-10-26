# .NET Aspire / cloud-native local development

This solution is orchestrated with **.NET Aspire 9**. The existing layered projects (`Domain`, `Infrastructure`, `Application`, `API`) are preserved; Aspire adds:

| Project | Role |
|---------|------|
| `NAITSSMS.AppHost` | Orchestrator / dashboard entry point |
| `NAITSSMS.ServiceDefaults` | Shared OpenTelemetry, health checks, service discovery, HttpClient resilience |
| `NAITSSMS.API` | Existing Web API (now references ServiceDefaults + Dockerfile) |

## Prerequisites

- .NET 9 SDK
- Docker Desktop (required for Aspire dashboard resource networking and container workflows)
- Access to the configured PostgreSQL database (`ConnectionStrings:DefaultConnection`)

## Build

From `NaitsSmsApi/`:

```powershell
dotnet restore NAITSSMS.sln
dotnet build NAITSSMS.sln -c Release
```

## Run with .NET Aspire (recommended)

```powershell
dotnet run --project NAITSSMS.AppHost --launch-profile https
```

- Aspire Dashboard opens in the browser (resources, logs, traces, metrics).
- The API is listed as resource `naits-api` with HTTP(S) endpoints.
- Health: `/health` (ready) and `/alive` (liveness).

Swagger remains available in Development on the API endpoint (e.g. `/swagger`).

## Run API alone (without Aspire)

```powershell
dotnet run --project NAITSSMS.API --launch-profile https
```

## Run API in Docker

```powershell
docker build -f NAITSSMS.API/Dockerfile -t naits-api .
docker run --rm -p 8080:8080 `
  -e ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." `
  -e JWT__Secret="..." `
  naits-api
```

Or:

```powershell
docker compose up --build
```

(Inject connection string / JWT via environment — do not commit secrets.)

## Debug

1. Set **NAITSSMS.AppHost** as the startup project in Visual Studio / Cursor / VS Code.
2. F5 / Debug — Aspire Dashboard + API launch together.
3. Set breakpoints in `NAITSSMS.API` or `NAITSSMS.Application` as usual.
4. Traces/logs appear in the Aspire Dashboard when OTLP is enabled (default under AppHost).

## Configuration

| Source | Purpose |
|--------|---------|
| `NAITSSMS.API/appsettings*.json` | Base settings |
| User secrets / env vars | Secrets (connection string, JWT) — preferred for containers |
| Aspire AppHost env | Injected when launched via AppHost |

Environment variable examples:

```text
ConnectionStrings__DefaultConnection=...
JWT__Secret=...
JWT__ValidIssuer=...
JWT__ValidAudience=...
MAP_ASPIRE_HEALTH_ENDPOINTS=true
```

## Database migrations

Unchanged — still against `NAITSSMS.Infrastructure`:

```powershell
dotnet ef database update --project NAITSSMS.Infrastructure --startup-project NAITSSMS.API
```

## Authentication

JWT Bearer and ASP.NET Identity configuration are unchanged. Ensure the same `JWT:*` settings are available to the process whether started via Aspire, `dotnet run`, or Docker.
