# Cloud Run OpenTelemetry Worker

Run a Temporal Worker on a
[Google Cloud Run worker pool](https://cloud.google.com/run/docs/deploy-worker-pools) that exports
Core SDK metrics and traces to a
[Google-Built OpenTelemetry Collector](https://cloud.google.com/stackdriver/docs/instrumentation/opentelemetry-collector-cloud-run)
sidecar (metrics to Managed Service for Prometheus, traces to Cloud Trace) via the
`Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry` extension.

`Program.cs` calls `ApplyGoogleCloudRunOpenTelemetryDefaults()`, which adds the tracing interceptor
and an OTLP exporter aimed at the sidecar, then runs a greeting workflow and activity until SIGTERM.

> The extension is not on nuget.org yet, so the sample restores it from the committed
> `local-packages/` feed (see `nuget.config`) until it ships.

## Prerequisites

- A Temporal server the worker pool can reach (`TEMPORAL_ADDRESS` / `TEMPORAL_NAMESPACE`)
- A Google Cloud project with the Cloud Run and Artifact Registry APIs enabled
- [`gcloud`](https://cloud.google.com/sdk/docs/install), authenticated with the project set
- The [Temporal CLI](https://docs.temporal.io/cli) and .NET 8

## Deploy

Set the placeholders used by `worker-pool.yaml`, then build the image, store the collector config as
a secret, and deploy. Run from the repo root:

```bash
export REGION=us-central1 WORKER_POOL=temporal-otel-worker INSTANCE_COUNT=1
export SERVICE_ACCOUNT_EMAIL=<sa>@<project>.iam.gserviceaccount.com
export WORKER_IMAGE=$REGION-docker.pkg.dev/$(gcloud config get-value project)/samples/cloud-run-otel
export TEMPORAL_ADDRESS=<host:7233> TEMPORAL_NAMESPACE=<namespace> TEMPORAL_TASK_QUEUE=cloud-run-worker
export COLLECTOR_CONFIG_SECRET=otel-collector-config COLLECTOR_CONFIG_SECRET_VERSION=latest

docker build -f src/Gcp/CloudRun/OpenTelemetry/Dockerfile -t "$WORKER_IMAGE" . && docker push "$WORKER_IMAGE"
gcloud secrets create "$COLLECTOR_CONFIG_SECRET" --data-file=src/Gcp/CloudRun/OpenTelemetry/collector-config.yaml

envsubst < src/Gcp/CloudRun/OpenTelemetry/worker-pool.yaml > /tmp/worker-pool.yaml
gcloud run worker-pools replace /tmp/worker-pool.yaml --region "$REGION"
```

The service account needs the `monitoring.metricWriter`, `cloudtrace.agent`, and
`secretmanager.secretAccessor` roles.

## Run a workflow

```bash
temporal workflow execute --task-queue cloud-run-worker --type GreetingWorkflow --input '"Temporal"'
```

Metrics appear in Metrics Explorer and traces in Trace Explorer. Delete the pool with
`gcloud run worker-pools delete "$WORKER_POOL" --region "$REGION"`.
