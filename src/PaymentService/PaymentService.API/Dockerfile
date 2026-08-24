# Build context is src/PaymentService/ (see docker/docker-compose.yml) so
# project references between the five PaymentService.* projects resolve.

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

COPY PaymentService.API/PaymentService.API.csproj PaymentService.API/
COPY PaymentService.Application/PaymentService.Application.csproj PaymentService.Application/
COPY PaymentService.Domain/PaymentService.Domain.csproj PaymentService.Domain/
COPY PaymentService.Infrastructure/PaymentService.Infrastructure.csproj PaymentService.Infrastructure/
RUN dotnet restore PaymentService.API/PaymentService.API.csproj

COPY PaymentService.API/ PaymentService.API/
COPY PaymentService.Application/ PaymentService.Application/
COPY PaymentService.Domain/ PaymentService.Domain/
COPY PaymentService.Infrastructure/ PaymentService.Infrastructure/
RUN dotnet publish PaymentService.API/PaymentService.API.csproj -c Release -o /app/publish --no-restore

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

ENTRYPOINT ["dotnet", "PaymentService.API.dll"]
