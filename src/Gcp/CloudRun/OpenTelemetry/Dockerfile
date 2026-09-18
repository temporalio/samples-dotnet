# syntax=docker/dockerfile:1
# Build from the repo root: docker build -f src/Gcp/CloudRun/OpenTelemetry/Dockerfile -t <image> .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig nuget.config ./
COPY src/Gcp/CloudRun/OpenTelemetry/ ./src/Gcp/CloudRun/OpenTelemetry/
RUN dotnet publish src/Gcp/CloudRun/OpenTelemetry/TemporalioSamples.Gcp.CloudRun.OpenTelemetry.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN useradd --create-home --uid 10001 worker
WORKDIR /app
COPY --from=build --chown=worker:worker /app ./
USER 10001
ENTRYPOINT ["dotnet", "TemporalioSamples.Gcp.CloudRun.OpenTelemetry.dll"]
