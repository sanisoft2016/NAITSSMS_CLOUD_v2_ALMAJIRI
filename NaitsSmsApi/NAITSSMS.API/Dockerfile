# NAITSSMS.API — multi-stage container image for .NET Aspire / Docker
# Build from repo folder: NaitsSmsApi
#   docker build -f NAITSSMS.API/Dockerfile -t naits-api .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NAITSSMS.Domain/NAITSSMS.Domain.csproj NAITSSMS.Domain/
COPY NAITSSMS.Infrastructure/NAITSSMS.Infrastructure.csproj NAITSSMS.Infrastructure/
COPY NAITSSMS.Application/NAITSSMS.Application.csproj NAITSSMS.Application/
COPY NAITSSMS.ServiceDefaults/NAITSSMS.ServiceDefaults.csproj NAITSSMS.ServiceDefaults/
COPY NAITSSMS.API/NAITSSMS.API.csproj NAITSSMS.API/

RUN dotnet restore NAITSSMS.API/NAITSSMS.API.csproj

COPY NAITSSMS.Domain/ NAITSSMS.Domain/
COPY NAITSSMS.Infrastructure/ NAITSSMS.Infrastructure/
COPY NAITSSMS.Application/ NAITSSMS.Application/
COPY NAITSSMS.ServiceDefaults/ NAITSSMS.ServiceDefaults/
COPY NAITSSMS.API/ NAITSSMS.API/

WORKDIR /src/NAITSSMS.API
# Re-run restore at publish time so linux-x64 assets exist when building
# with --platform linux/amd64 (a host-copied obj/ folder is RID-specific).
RUN dotnet publish NAITSSMS.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV MAP_ASPIRE_HEALTH_ENDPOINTS=true
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NAITSSMS.API.dll"]
