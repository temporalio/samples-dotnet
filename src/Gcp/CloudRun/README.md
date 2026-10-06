# Cloud Run Worker

Run a Temporal Worker on a
[Google Cloud Run worker pool](https://cloud.google.com/run/docs/deploy-worker-pools) using both GCP
Cloud Run extensions together:

- `Temporalio.Extensions.Gcp.CloudRun.Id` derives the client identity `{instanceId}@{revision}` from
  Cloud Run instance metadata, so each instance is identifiable in Temporal.
- `Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry` exports Core SDK metrics and traces to a
  [Google-Built OpenTelemetry Collector](https://cloud.google.com/stackdriver/docs/instrumentation/opentelemetry-collector-cloud-run)
  sidecar (metrics to Managed Service for Prometheus, traces to Cloud Trace).

`Program.cs` registers a `CloudRunIdPlugin` on `TemporalClientConnectOptions.Plugins` and calls
`ApplyGoogleCloudRunOpenTelemetryDefaults()`, which adds the tracing interceptor and an OTLP exporter
aimed at the sidecar, then runs a greeting workflow and activity until SIGTERM.

## Prerequisites

- A Temporal server the worker pool can reach (`TEMPORAL_ADDRESS` / `TEMPORAL_NAMESPACE`)
- A Google Cloud project with the Cloud Run and Artifact Registry APIs enabled
- [`gcloud`](https://cloud.google.com/sdk/docs/install), authenticated with the project set
- The [Temporal CLI](https://docs.temporal.io/cli) and .NET 10

## Deploy

Set the placeholders used by `worker-pool.yaml`, then build the image, store the collector config as
a secret, and deploy. Run from the repo root:

```bash
export REGION=us-central1 WORKER_POOL=temporal-cloud-run-worker INSTANCE_COUNT=1
export SERVICE_ACCOUNT_EMAIL=<sa>@<project>.iam.gserviceaccount.com
export WORKER_IMAGE=$REGION-docker.pkg.dev/$(gcloud config get-value project)/samples/cloud-run
export TEMPORAL_ADDRESS=<host:7233> TEMPORAL_NAMESPACE=<namespace> TEMPORAL_TASK_QUEUE=cloud-run-worker
export COLLECTOR_CONFIG_SECRET=otel-collector-config COLLECTOR_CONFIG_SECRET_VERSION=latest
# Temporal Cloud: the API key the worker authenticates with, stored in Secret Manager.
export TEMPORAL_API_KEY_SECRET=temporal-api-key TEMPORAL_API_KEY_SECRET_VERSION=latest

# One-time setup: Artifact Registry repo, collector-config secret, and Temporal Cloud API key secret.
gcloud artifacts repositories create samples --repository-format=docker --location "$REGION"
gcloud secrets create "$COLLECTOR_CONFIG_SECRET" --data-file=src/Gcp/CloudRun/collector-config.yaml
# Temporal Cloud: store the API key (from `tcld apikey create ...`) so the worker can read it.
printf '%s' "<temporal-cloud-api-key>" | gcloud secrets create "$TEMPORAL_API_KEY_SECRET" --data-file=-

# Build, push, and deploy (re-run to update). Cloud Run requires linux/amd64 images, so build for
# that platform explicitly (on arm64 Macs a native build would produce an unrunnable arm64 image).
docker build --platform linux/amd64 -f src/Gcp/CloudRun/Dockerfile -t "$WORKER_IMAGE" . && docker push "$WORKER_IMAGE"
envsubst < src/Gcp/CloudRun/worker-pool.yaml > /tmp/worker-pool.yaml
gcloud run worker-pools replace /tmp/worker-pool.yaml
```

The service account needs the `monitoring.metricWriter`, `cloudtrace.agent`, and
`secretmanager.secretAccessor` roles. The worker reads `TEMPORAL_API_KEY` from the Secret Manager
secret above (see `worker-pool.yaml`) to authenticate to Temporal Cloud.

## Run a workflow

```bash
temporal workflow execute --task-queue cloud-run-worker --type GreetingWorkflow --input '"Temporal"'
```

Metrics appear in Metrics Explorer and traces in Trace Explorer, tagged with the Cloud Run identity.
Delete the pool with `gcloud run worker-pools delete "$WORKER_POOL" --region "$REGION"`.
