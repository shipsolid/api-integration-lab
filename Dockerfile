# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore from the project file first so source-only edits reuse the expensive NuGet layer.
COPY src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj src/ApiIntegrationLab.Api/
# Corporate TLS interception may require:
# docker build --secret id=ca_bundle,src=/etc/ssl/certs/ca-certificates.crt ...
# The secret is optional, exists only for this layer, and is never copied into the resulting image.
RUN --mount=type=secret,id=ca_bundle,target=/tmp/ca-bundle.crt,required=false \
    if [ -f /tmp/ca-bundle.crt ]; then export SSL_CERT_FILE=/tmp/ca-bundle.crt; fi; \
    dotnet restore src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj

COPY src/ApiIntegrationLab.Api/ src/ApiIntegrationLab.Api/
RUN dotnet publish src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# The runtime image has no HTTP probe binary; install only curl and remove package indexes.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# .NET 8 images define an unprivileged app user. Provider secrets are therefore never exposed to root code.
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ApiIntegrationLab.Api.dll"]
