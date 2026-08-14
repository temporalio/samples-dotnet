# syntax=docker/dockerfile:1
#
# Builds the Cloud Run worker image. Temporalio (with its bundled native bridge for linux) is
# restored from NuGet; the GCP Cloud Run OpenTelemetry package is restored from the local folder
# feed under this sample until it is published (see nuget.config / local-packages). No SDK-from-
# source or Rust build is needed.
#
# Build context is the samples-dotnet repo root (so the shared Directory.*.props / global.json are
# available):
#   docker build -f src/CloudRunWorker/Dockerfile -t <image> .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/CloudRunWorker/ ./src/CloudRunWorker/
RUN dotnet publish src/CloudRunWorker/TemporalioSamples.CloudRunWorker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN useradd --create-home --uid 10001 worker
WORKDIR /app
COPY --from=build --chown=worker:worker /app ./
USER 10001
ENTRYPOINT ["dotnet", "TemporalioSamples.CloudRunWorker.dll"]
