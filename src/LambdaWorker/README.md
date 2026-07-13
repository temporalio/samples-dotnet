# Lambda Worker

This sample demonstrates how to run a Temporal Worker inside an AWS Lambda
function using the `Temporalio.Extensions.Aws.Lambda` package. It includes
OpenTelemetry instrumentation that exports traces and metrics through AWS Distro
for OpenTelemetry (ADOT).

The sample registers a simple greeting Workflow and Activity, but the pattern
applies to any Workflow/Activity definitions.

## Prerequisites

- A [Temporal Cloud](https://temporal.io/cloud) namespace (or a self-hosted
  Temporal cluster accessible from your Lambda)
- AWS CLI configured with permissions to create Lambda functions, IAM roles, and
  CloudFormation stacks
- A Temporal API key stored in the Lambda function's `TEMPORAL_API_KEY`
  environment variable
- .NET 8

## Files

| File | Description |
|------|-------------|
| `Function.cs` | Lambda entry point that configures the worker, registers Workflows/Activities, and exports the handler |
| `SampleWorkflow.workflow.cs` | Sample Workflow that executes a greeting Activity |
| `Activities.cs` | Sample Activity that returns a greeting string |
| `Program.cs` | Helper program to start a Workflow execution from a local machine |
| `temporal.toml` | Temporal client connection configuration (update with your namespace) |
| `otel-collector-config.yaml` | OpenTelemetry Collector sidecar configuration for ADOT |
| `deploy-lambda.sh` | Packages and deploys the Lambda function |
| `mk-iam-role.sh` | Creates the IAM role that allows Temporal Cloud to invoke the Lambda |
| `iam-role-for-temporal-lambda-invoke-test.yaml` | CloudFormation template for the IAM role |
| `extra-setup-steps` | Additional IAM and Lambda configuration for OpenTelemetry support |

## Setup

The instructions here are a slimmed down version of the more complete getting
started guide, which you can find
[here](https://docs.temporal.io/production-deployment/worker-deployments/serverless-workers/aws-lambda).

### 1. Create a Lambda function for your .NET worker

Use either the AWS web UI or CLI to create a .NET 8 runtime Lambda function. Ex:

```bash
aws lambda create-function \
  --function-name my-temporal-worker \
  --runtime dotnet8 \
  --handler TemporalioSamples.LambdaWorker::TemporalioSamples.LambdaWorker.LambdaFunction::HandlerAsync \
  --role arn:aws:iam::<YOUR_ACCOUNT_ID>:role/my-temporal-worker-execution \
  --timeout 600 \
  --memory-size 256
```

The handler uses the .NET Lambda class/method handler convention. This differs
from the Python sample's module-level handler and the TypeScript sample's
bundled handler, but it exposes the same Temporal Lambda worker behavior.

### 2. Configure Temporal connection

Edit `temporal.toml` with your Temporal Cloud namespace address and namespace.
The sample reads the API key from the `TEMPORAL_API_KEY` environment variable
instead of bundling credentials with the Lambda code. When an API key is
present, the .NET SDK enables TLS automatically.

The Lambda worker loads Temporal client configuration in this order:

1. `TEMPORAL_CONFIG_FILE`, if set.
2. `temporal.toml` from the Lambda task root, when running in Lambda.
3. `temporal.toml` from the current working directory.

The config loader applies environment variable overrides, including:

- `TEMPORAL_ADDRESS`
- `TEMPORAL_NAMESPACE`
- `TEMPORAL_API_KEY`

Set the API key on the Lambda function:

```bash
aws lambda update-function-configuration \
  --function-name my-temporal-worker \
  --environment "Variables={TEMPORAL_API_KEY=<your-api-key>,SSL_CERT_FILE=/etc/pki/tls/certs/ca-bundle.crt}" \
  --query '{FunctionName:FunctionName,LastUpdateStatus:LastUpdateStatus,RevisionId:RevisionId}' \
  --output json
```

If you also enable OpenTelemetry, include
`OPENTELEMETRY_COLLECTOR_CONFIG_URI=/var/task/otel-collector-config.yaml` in the
same Lambda environment configuration. `SSL_CERT_FILE` works around CA loading
behavior in the .NET Lambda runtime that can otherwise produce
`NativeCertsNotFound` when connecting to Temporal Cloud.

### 3. Create the IAM role

This creates the IAM role that Temporal Cloud assumes to invoke your Lambda
function:

```bash
./mk-iam-role.sh <stack-name> <external-id> <lambda-arn>
```

The External ID is provided by Temporal Cloud in your namespace's serverless
worker configuration.

### 4. Enable OpenTelemetry

The sample calls `ApplyOpenTelemetryDefaults` in `Function.cs`.
If you want traces and metrics, attach the ADOT Collector layer to your
Lambda function. You will need to add the appropriate layer for your runtime and
region. See
[this page](https://aws-otel.github.io/docs/getting-started/lambda#getting-started-with-aws-lambda-layers)
for more info.

Set this environment variable on the Lambda function so the ADOT Collector uses
the bundled config:

```bash
OPENTELEMETRY_COLLECTOR_CONFIG_URI=/var/task/otel-collector-config.yaml
```

Then run the extra setup to grant the Lambda role the necessary permissions:

```bash
./extra-setup-steps <role-name> <function-name> <region> <account-id>
```

The bundled `otel-collector-config.yaml` uses Lambda's `AWS_REGION` and
`AWS_LAMBDA_FUNCTION_NAME` environment variables, so it does not need edits for
the normal single-function test deployment.

### 5. Deploy the Lambda function

```bash
./deploy-lambda.sh <function-name>
```

This runs `dotnet publish`, bundles the publish output with your code and
configuration files, and uploads to AWS Lambda.

The script publishes for `linux-x64` by default. Set
`TEMPORAL_DOTNET_LAMBDA_RUNTIME=linux-arm64` when the Lambda function uses the
Arm64 architecture.

### 6. Configure Temporal to invoke your Lambda function

Refer to the docs
[here](https://docs.temporal.io/production-deployment/worker-deployments/serverless-workers/aws-lambda#create-worker-deployment-version).

The worker deployment version in this sample is:

- Deployment name: `my-app`
- Build ID: `build-1`

If you create the worker deployment version through the Temporal UI, it is set
as current automatically. If you create it through the Temporal CLI, set it as
current before starting a Workflow:

```bash
temporal --config-file temporal.toml --profile default --api-key "$TEMPORAL_API_KEY" \
  worker deployment set-current-version \
  --deployment-name my-app \
  --build-id build-1 \
  --allow-no-pollers \
  --yes
```

`--allow-no-pollers` is expected for this sample because the Lambda worker has
no long-running pollers before Temporal invokes the function.

You can verify the deployment routing state with:

```bash
temporal --config-file temporal.toml --profile default --api-key "$TEMPORAL_API_KEY" \
  worker deployment describe \
  --name my-app
```

To verify that Temporal can assume the IAM role and invoke the Lambda function,
open Workers > Deployments, select the deployment, open the Actions menu on the
version, and click Validate Connection.

For a direct Lambda smoke test, temporarily lower the function timeout first.
The worker normally runs until shortly before the Lambda deadline, so directly
invoking a 600-second function can exceed the AWS CLI read timeout even when the
worker is healthy:

```bash
ORIGINAL_TIMEOUT=$(aws lambda get-function-configuration \
  --function-name my-temporal-worker \
  --query Timeout \
  --output text)

aws lambda update-function-configuration \
  --function-name my-temporal-worker \
  --timeout 30 \
  --query '{FunctionName:FunctionName,Timeout:Timeout,LastUpdateStatus:LastUpdateStatus}' \
  --output json

aws lambda wait function-updated \
  --function-name my-temporal-worker

aws lambda invoke \
  --function-name my-temporal-worker \
  --cli-binary-format raw-in-base64-out \
  --cli-read-timeout 60 \
  --payload '{}' \
  /tmp/temporal-lambda-bootstrap.json

aws lambda update-function-configuration \
  --function-name my-temporal-worker \
  --timeout "$ORIGINAL_TIMEOUT" \
  --query '{FunctionName:FunctionName,Timeout:Timeout,LastUpdateStatus:LastUpdateStatus}' \
  --output json
```

### 7. Start a Workflow

Use the starter program to execute a Workflow on the Lambda worker, using the
same config file the Lambda uses for connecting to the server.

From inside this directory:

```bash
TEMPORAL_CONFIG_FILE=temporal.toml TEMPORAL_API_KEY=<your-api-key> \
  mise exec dotnet@8 -- dotnet run --project TemporalioSamples.LambdaWorker.csproj -- workflow
```
