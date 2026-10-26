# Aspire migration — change summary

## Significant changes

1. **Added `NAITSSMS.AppHost`**  
   Orchestrates the API as Aspire resource `naits-api` with external HTTP endpoints and `/health` checks. Reason: central cloud-native orchestration, dashboard, and local multi-service readiness.

2. **Added `NAITSSMS.ServiceDefaults`**  
   Shared OpenTelemetry (OTLP), health checks, service discovery, and HttpClient resilience. Reason: Aspire best-practice shared defaults without duplicating boilerplate in every service.

3. **Updated `NAITSSMS.API/Program.cs`**  
   Calls `AddServiceDefaults()` / `MapDefaultEndpoints()`, Npgsql health check, safer connection/JWT null guards, removed duplicate AutoMapper registration. Reason: wire Aspire observability while preserving Identity, JWT, EF, CORS, and controllers.

4. **Removed `Microsoft.AspNet.WebApi.Core`** from API  
   Legacy .NET Framework package unused by ASP.NET Core controllers. Reason: incompatible with cloud-native .NET 9.

5. **Removed `Microsoft.AspNet.Mvc`** from Application  
   Replaced `System.Web.Mvc` usage in `AssessoriesDto.cs` with the existing `NAITSSMS.Application.DTOs.SelectListItem`. Reason: eliminate Framework-era dependency.

6. **Added `NAITSSMS.API/Dockerfile` + `.dockerignore` + `docker-compose.yml`**  
   Multi-stage Linux container for the API; Compose for optional standalone runs. Reason: containerize the service for Aspire/Docker workflows.

7. **Health endpoints**  
   `/health` and `/alive` mapped in Development, containers, or when `MAP_ASPIRE_HEALTH_ENDPOINTS=true`. Reason: Aspire orchestration and container probes.

8. **EF Core OpenTelemetry instrumentation** (ServiceDefaults)  
   Traces database calls via OpenTelemetry. Reason: cloud-native observability for DB interactions.

9. **Documentation `ASPIRE.md`**  
   How to build, run, debug with Aspire and Docker.

## Preserved

- Project layering (Domain / Infrastructure / Application / API)
- All API controllers and business services
- EF Core + PostgreSQL + migrations assembly
- ASP.NET Identity + JWT authentication
- CORS, static files, Swagger (Development)
- Existing appsettings-driven configuration model
