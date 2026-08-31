# Cloud Run Worker

This sample demonstrates how to run a long-lived Temporal Worker on
[Google Cloud Run](https://cloud.google.com/run) and derive its identity and
[Worker Deployment Version](https://docs.temporal.io/worker-deployments) from
Cloud Run metadata using the `Temporalio.Extensions.Gcp.CloudRun` package.

The sample registers a greeting Workflow and Activity and polls until the
container is stopped.

## How it works

Unlike the AWS Lambda extension, Cloud Run runs a long-lived container, so this
is a metadata-driven plugin rather than a worker wrapper. The sample registers a
single `WorkerIdPlugin` on `TemporalClientConnectOptions.Plugins`. Because it is
both a client and a worker plugin, registering it once is enough:

1. At connect time its client hook reads the instance id from the Cloud Run
   metadata server and the deployment name and revision from the environment,
   then sets the client `Identity` to `{instanceId}@{revision}` (only when an
   identity is not already set).
2. When the worker is created its worker hook turns on Worker Versioning with a
   Worker Deployment Version whose deployment name is the Cloud Run name and
   whose build id is the Cloud Run revision, and sets the default versioning
   behavior to `Pinned`.

The name and revision come from the environment that Cloud Run injects:

| Value    | Worker pool variable   | Service variable |
| -------- | ---------------------- | ---------------- |
| Name     | `CLOUD_RUN_WORKER_POOL`| `K_SERVICE`      |
| Revision | `CLOUD_RUN_REVISION`   | `K_REVISION`     |

The worker-pool variables take precedence, so the same code works on a Cloud Run
[worker pool](https://cloud.google.com/run/docs/deploy-worker-pools) or a Cloud
Run service.

### Why worker pools

A Cloud Run **worker pool** runs one or more long-lived container instances that
receive no inbound HTTP requests and are not scaled by request traffic, which is
exactly the shape of a Temporal Worker that polls a Task Queue. Each revision of
a worker pool maps cleanly onto a Temporal Worker Deployment Version: deploying a
new revision creates a new build id, and pinning keeps in-flight Workflows on the
revision that started them until you roll traffic forward.

## Unreleased dependency

`Temporalio.Extensions.Gcp.CloudRun` is not published to NuGet yet, so this
sample cannot be built or deployed from a released package. To let it build
locally, `TemporalioSamples.CloudRunWorker.csproj` references the SDK from a
sibling checkout of [sdk-dotnet](https://github.com/temporalio/sdk-dotnet) laid
out next to this repository:

```text
temporalio/
  samples-dotnet/     # this repo
  sdk-dotnet-2/       # https://github.com/temporalio/sdk-dotnet on branch cloud-run-worker-id
```

Once the package is released, delete the temporary item groups in the `.csproj`
(the `Temporalio*` `PackageReference Remove` entries and the `ProjectReference`
entries) and replace them with:

```xml
<ItemGroup>
  <PackageReference Include="Temporalio.Extensions.Gcp.CloudRun" />
</ItemGroup>
```

This is why the pull request that adds this sample is a draft.

## Prerequisites

- A [Temporal Cloud](https://temporal.io/cloud) namespace, or a self-hosted
  Temporal cluster the worker pool can reach
- A Google Cloud project with billing enabled and the Cloud Run API enabled
- The [`gcloud` CLI](https://cloud.google.com/sdk/docs/install), authenticated
  (`gcloud auth login`) with the project set (`gcloud config set project ...`)
- The [Temporal CLI](https://docs.temporal.io/cli)
- .NET 8 to build locally

## Configuration

The worker reads its connection settings from the environment; Cloud Run injects
these through `--set-env-vars`:

| Variable              | Default                    | Description                         |
| --------------------- | -------------------------- | ----------------------------------- |
| `TEMPORAL_ADDRESS`    | `localhost:7233`           | Temporal frontend `host:port`.      |
| `TEMPORAL_NAMESPACE`  | `default`                  | Temporal namespace.                 |
| `TEMPORAL_TASK_QUEUE` | `cloud-run-worker-sample`  | Task Queue the worker polls.        |

This sample uses a plaintext connection for brevity. For Temporal Cloud, add API
key / mTLS configuration to `Program.cs` before deploying.

## 1. Deploy to a Cloud Run worker pool

Deploy the sample as a worker pool from source (Cloud Build packages the
container with the .NET buildpack). Worker pools are currently a preview feature
and may require the `beta`/`alpha` track:

```bash
export REGION="us-central1"
export WORKER_POOL="temporal-dotnet-worker"

gcloud run worker-pools deploy "$WORKER_POOL" \
  --source . \
  --region "$REGION" \
  --set-env-vars \
TEMPORAL_ADDRESS=<your-namespace>.<account>.tmprl.cloud:7233,TEMPORAL_NAMESPACE=<your-namespace>.<account>,TEMPORAL_TASK_QUEUE=cloud-run-worker-sample
```

Run this from `src/CloudRunWorker`. Cloud Run sets `CLOUD_RUN_WORKER_POOL` to
`$WORKER_POOL` and `CLOUD_RUN_REVISION` to the revision it creates, which the
plugin turns into the worker identity and Worker Deployment Version. Deploying
again creates a new revision, and therefore a new build id.

## 2. Route the Worker Deployment Version

Once the worker pool is polling, point the deployment's current version at the
revision so pinned Workflows are routed to it. The deployment name is the worker
pool name and the build id is the Cloud Run revision, which
`gcloud run worker-pools describe` reports:

```bash
export DEPLOYMENT_NAME="$WORKER_POOL"
export BUILD_ID="$(gcloud run worker-pools describe "$WORKER_POOL" \
  --region "$REGION" \
  --format 'value(status.latestReadyRevisionName)')"

temporal worker deployment set-current-version \
  --deployment-name "$DEPLOYMENT_NAME" \
  --build-id "$BUILD_ID" \
  --yes
```

Verify the routing state:

```bash
temporal worker deployment describe --name "$DEPLOYMENT_NAME"
```

## 3. Start a Workflow

Start the greeting Workflow on the same Task Queue and wait for the result:

```bash
temporal workflow execute \
  --task-queue cloud-run-worker-sample \
  --type SampleWorkflow \
  --workflow-id cloud-run-worker-sample-1 \
  --input '"Cloud Run"'
```

A successful run returns `"Hello, Cloud Run!"`.

## 4. Clean up

Reset Temporal routing and delete the worker pool:

```bash
temporal worker deployment set-current-version \
  --deployment-name "$DEPLOYMENT_NAME" \
  --unversioned \
  --yes

gcloud run worker-pools delete "$WORKER_POOL" --region "$REGION"
```

## Build locally

With the sibling `sdk-dotnet-2` checkout in place (see
[Unreleased dependency](#unreleased-dependency)):

```bash
dotnet build src/CloudRunWorker
```

Running the worker outside Cloud Run fails at startup because the Cloud Run
metadata server is unreachable; deploy it to a worker pool (or service) to run
it.
