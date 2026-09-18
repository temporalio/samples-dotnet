# Cloud Run Id

Run a Temporal Worker on a
[Google Cloud Run worker pool](https://cloud.google.com/run/docs/deploy-worker-pools) and derive its
identity from Cloud Run instance metadata using the `Temporalio.Extensions.Gcp.CloudRun.Id` package.

`Program.cs` registers a `CloudRunIdPlugin` on `TemporalClientConnectOptions.Plugins`. At connect
time the plugin sets the client `Identity` to `{instanceId}@{revision}` from the Cloud Run metadata
server and environment, and every Worker created from the client inherits it. A greeting workflow and
activity poll the task queue until the container stops. Worker pools run instances that take no HTTP
traffic, matching a polling Worker; the same code also works on a Cloud Run service.

> The extension is not on nuget.org yet, so the sample restores it from the committed
> `local-packages/` feed (see `nuget.config`) until it ships.

## Prerequisites

- A Temporal server the worker pool can reach
- A Google Cloud project with billing and the Cloud Run and Artifact Registry APIs enabled
- [`gcloud`](https://cloud.google.com/sdk/docs/install), authenticated with the project set
- The [Temporal CLI](https://docs.temporal.io/cli) and .NET 8

## Deploy

Build the image from the repo root (the Dockerfile pulls in the shared props and the local feed),
push it, then deploy a worker pool. Worker pools may require the `beta` track.

```bash
export REGION=us-central1 WORKER_POOL=temporal-dotnet-worker
export WORKER_IMAGE=$REGION-docker.pkg.dev/$(gcloud config get-value project)/samples/cloud-run-id

docker build -f src/Gcp/CloudRun/Id/Dockerfile -t "$WORKER_IMAGE" . && docker push "$WORKER_IMAGE"

gcloud run worker-pools deploy "$WORKER_POOL" --image "$WORKER_IMAGE" --region "$REGION" \
  --set-env-vars TEMPORAL_ADDRESS=<host:7233>,TEMPORAL_NAMESPACE=<namespace>,TEMPORAL_TASK_QUEUE=cloud-run-worker-sample
```

Cloud Run sets `CLOUD_RUN_WORKER_POOL` and `CLOUD_RUN_REVISION`, which the plugin turns into the
client identity. The sample connects in plaintext; for Temporal Cloud add API key / mTLS to
`Program.cs`.

## Run a workflow

```bash
temporal workflow execute --task-queue cloud-run-worker-sample --type SampleWorkflow --input '"Cloud Run"'
```

A successful run returns `"Hello, Cloud Run!"`. Delete the pool with
`gcloud run worker-pools delete "$WORKER_POOL" --region "$REGION"`.
