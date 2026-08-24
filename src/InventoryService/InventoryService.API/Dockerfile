# Build context is src/InventoryService/ (see docker/docker-compose.yml) so
# project references between the five InventoryService.* projects resolve.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# curl + HEALTHCHECK are duplicated from the "final" stage below: the "build"
# stage is also run directly in dev mode (docker-compose.override.yml targets
# it with `dotnet watch run`), and depends_on: condition: service_healthy
# elsewhere in Compose requires every stage that can be started to define one.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

COPY InventoryService.API/InventoryService.API.csproj InventoryService.API/
COPY InventoryService.Application/InventoryService.Application.csproj InventoryService.Application/
COPY InventoryService.Domain/InventoryService.Domain.csproj InventoryService.Domain/
COPY InventoryService.Infrastructure/InventoryService.Infrastructure.csproj InventoryService.Infrastructure/
RUN dotnet restore InventoryService.API/InventoryService.API.csproj

COPY InventoryService.API/ InventoryService.API/
COPY InventoryService.Application/ InventoryService.Application/
COPY InventoryService.Domain/ InventoryService.Domain/
COPY InventoryService.Infrastructure/ InventoryService.Infrastructure/
RUN dotnet publish InventoryService.API/InventoryService.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "InventoryService.API.dll"]
