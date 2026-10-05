# syntax=docker/dockerfile:1
# Build from the repo root: docker build -f src/Gcp/CloudRun/Dockerfile -t <image> .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Gcp/CloudRun/ ./src/Gcp/CloudRun/
RUN dotnet publish src/Gcp/CloudRun/TemporalioSamples.Gcp.CloudRun.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN useradd --create-home --uid 10001 worker
WORKDIR /app
COPY --from=build --chown=worker:worker /app ./
USER 10001
ENTRYPOINT ["dotnet", "TemporalioSamples.Gcp.CloudRun.dll"]
