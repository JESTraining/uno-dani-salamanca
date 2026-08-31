# Build context is src/OrderService/ (see docker/docker-compose.yml) so
# project references between the five OrderService.* projects resolve.

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

COPY OrderService.API/OrderService.API.csproj OrderService.API/
COPY OrderService.Application/OrderService.Application.csproj OrderService.Application/
COPY OrderService.Domain/OrderService.Domain.csproj OrderService.Domain/
COPY OrderService.Infrastructure/OrderService.Infrastructure.csproj OrderService.Infrastructure/
RUN dotnet restore OrderService.API/OrderService.API.csproj

COPY OrderService.API/ OrderService.API/
COPY OrderService.Application/ OrderService.Application/
COPY OrderService.Domain/ OrderService.Domain/
COPY OrderService.Infrastructure/ OrderService.Infrastructure/
RUN dotnet publish OrderService.API/OrderService.API.csproj -c Release -o /app/publish --no-restore

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

ENTRYPOINT ["dotnet", "OrderService.API.dll"]
