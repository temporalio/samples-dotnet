# Lambda Worker

This sample demonstrates how to run a Temporal Worker inside an AWS Lambda
function using the `Temporalio.Extensions.Aws.Lambda` package. It exports
traces and metrics through AWS Distro for OpenTelemetry (ADOT).

The sample registers a greeting Workflow and Activity. A separate starter
project starts the Workflow from a local machine; starter-only code is not
included in the Lambda deployment package.

## Prerequisites

- A [Temporal Cloud](https://temporal.io/cloud) namespace, or a self-hosted
  Temporal cluster that the Lambda function can reach
- A Temporal API key for that namespace, stored in a local file outside this
  repository
- The Temporal CLI
- AWS CLI credentials with permission to manage Lambda, IAM, CloudFormation,
  CloudWatch Logs, CloudWatch metrics, and X-Ray in the target account
- .NET 8, `zip`, and `openssl`
- An ADOT Collector Lambda layer ARN for the target region and architecture;
  see the
  [ADOT Lambda documentation](https://aws-otel.github.io/docs/getting-started/lambda#getting-started-with-aws-lambda-layers)

Run the commands below from `src/LambdaWorker`.

## Layout

- `Worker/` contains the Lambda handler, Workflow, Activity, and worker project.
- `Starter/` contains the local Workflow starter project.
- `Deploy/` contains the AWS deployment scripts and CloudFormation template.
- `temporal.template.toml` and `otel-collector-config.template.yaml` are the
  configuration templates copied in the setup below.

## 1. Choose fresh identifiers and AWS context

Use unique names for every dry run so existing deployments or concurrent tests
cannot affect the result:

```bash
export AWS_PROFILE="<your-aws-profile>"
export AWS_REGION="us-west-2"
export AWS_ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"

export SUFFIX="$(date -u +%Y%m%d%H%M%S)-$(openssl rand -hex 3)"
export FUNCTION_NAME="temporal-dotnet-lambda-${SUFFIX}"
export EXECUTION_ROLE_NAME="temporal-dotnet-exec-${SUFFIX}"
export STACK_NAME="tdnl-${SUFFIX}"
export DEPLOYMENT_NAME="dotnet-lambda-${SUFFIX}"
export BUILD_ID="build-${SUFFIX}"
export TASK_QUEUE="dotnet-lambda-tq-${SUFFIX}"
export WORKFLOW_ID_PREFIX="dotnet-lambda-wf-${SUFFIX}"
export EXTERNAL_ID="$(openssl rand -hex 16)"

export ADOT_LAYER_ARN="<adot-collector-layer-arn-for-${AWS_REGION}>"
export TEMPORAL_API_KEY_FILE="<path-to-temporal-api-key-file>"
export TEMPORAL_API_KEY="$(tr -d '\r\n' < "$TEMPORAL_API_KEY_FILE")"
```

The AWS CLI commands and scripts inherit `AWS_PROFILE` and `AWS_REGION`.
The same `EXTERNAL_ID` must be supplied to both AWS IAM and Temporal Cloud.

## 2. Create the local configuration files

The unsuffixed files are intentionally ignored because `temporal.toml`
contains namespace-specific configuration:

```bash
cp temporal.template.toml temporal.toml
cp otel-collector-config.template.yaml otel-collector-config.yaml
```

Edit `temporal.toml` and replace the address and namespace placeholders:

```toml
[profile.default]
address = "<your-namespace>.<account>.tmprl.cloud:7233"
namespace = "<your-namespace>.<account>"
```

The API key remains in `TEMPORAL_API_KEY`; do not add it to either file.
When an API key is present, the .NET SDK and Temporal CLI enable TLS
automatically.

The Lambda worker loads Temporal configuration in this order:

1. `TEMPORAL_CONFIG_FILE`, if set.
2. `temporal.toml` from the Lambda task root when running in Lambda.
3. `temporal.toml` from the current working directory.

## 3. Create the Lambda execution role and deploy

Create the IAM role assumed by the Lambda service:

```bash
./Deploy/mk-lambda-execution-role.sh "$EXECUTION_ROLE_NAME"

export EXECUTION_ROLE_ARN="$(aws iam get-role \
  --role-name "$EXECUTION_ROLE_NAME" \
  --query Role.Arn \
  --output text)"
```

Publish the worker for Linux and create the function:

```bash
./Deploy/deploy-lambda.sh "$FUNCTION_NAME" "$EXECUTION_ROLE_ARN"
```

The script defaults to `linux-x64` and creates an `x86_64` function. For
Arm64, set `TEMPORAL_DOTNET_LAMBDA_RUNTIME=linux-arm64`; the script then
creates an `arm64` function. On later runs, the same command updates the
existing function code and waits until the update is complete.

## 4. Configure Temporal and OpenTelemetry in Lambda

This command supplies all environment variables together because the Lambda
`--environment` option replaces the complete variable map. Its output is
restricted so the API key is never echoed:

```bash
aws lambda update-function-configuration \
  --function-name "$FUNCTION_NAME" \
  --timeout 60 \
  --layers "$ADOT_LAYER_ARN" \
  --environment "Variables={TEMPORAL_API_KEY=${TEMPORAL_API_KEY},SSL_CERT_FILE=/etc/pki/tls/certs/ca-bundle.crt,OPENTELEMETRY_COLLECTOR_CONFIG_URI=/var/task/otel-collector-config.yaml,TEMPORAL_TASK_QUEUE=${TASK_QUEUE},TEMPORAL_LAMBDA_DEPLOYMENT_NAME=${DEPLOYMENT_NAME},TEMPORAL_LAMBDA_BUILD_ID=${BUILD_ID}}" \
  --query '{FunctionName:FunctionName,Timeout:Timeout,Layers:Layers[*].Arn,LastUpdateStatus:LastUpdateStatus,RevisionId:RevisionId}' \
  --output json

aws lambda wait function-updated --function-name "$FUNCTION_NAME"
```

A 60-second timeout keeps a dry run short. For a production worker, choose a
timeout appropriate for the workload; the worker continues polling until
shortly before the Lambda deadline.

Grant the execution role permission to publish X-Ray traces and CloudWatch EMF
metrics, then enable active X-Ray tracing:

```bash
./Deploy/enable-telemetry.sh \
  "$EXECUTION_ROLE_NAME" \
  "$FUNCTION_NAME" \
  "$AWS_REGION" \
  "$AWS_ACCOUNT_ID"
```

The collector sends traces to `awsxray` and metrics to `awsemf`. It has no
diagnostic or logs exporter.

## 5. Create the role assumed by Temporal Cloud

Get the Lambda ARN and create a CloudFormation stack containing the invocation
role:

```bash
export LAMBDA_ARN="$(aws lambda get-function-configuration \
  --function-name "$FUNCTION_NAME" \
  --query FunctionArn \
  --output text)"

./Deploy/mk-iam-role.sh "$STACK_NAME" "$EXTERNAL_ID" "$LAMBDA_ARN"
```

The script waits for stack creation and prints the physical role name and ARN.
Capture the ARN for the Temporal deployment version:

```bash
export INVOKE_ROLE_ARN="$(aws cloudformation describe-stacks \
  --stack-name "$STACK_NAME" \
  --query "Stacks[0].Outputs[?OutputKey=='RoleARN'].OutputValue | [0]" \
  --output text)"
```

## 6. Create and route the Temporal Worker Deployment Version

The deployment name and build ID supplied to Temporal must match the Lambda
environment variables configured above:

```bash
temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment create \
  --name "$DEPLOYMENT_NAME"

temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment create-version \
  --deployment-name "$DEPLOYMENT_NAME" \
  --build-id "$BUILD_ID" \
  --aws-lambda-function-arn "$LAMBDA_ARN" \
  --aws-lambda-assume-role-arn "$INVOKE_ROLE_ARN" \
  --aws-lambda-assume-role-external-id "$EXTERNAL_ID"

temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment set-current-version \
  --deployment-name "$DEPLOYMENT_NAME" \
  --build-id "$BUILD_ID" \
  --allow-no-pollers \
  --yes
```

The deployment is created explicitly because the Lambda worker has no
long-running poller that could create it lazily. For the same reason,
`--allow-no-pollers` is expected when setting the current version. Verify the
routing state:

```bash
temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment describe \
  --name "$DEPLOYMENT_NAME"
```

You can also use Workers > Deployments > Actions > Validate Connection in the
Temporal Cloud UI.

## 7. Start a Workflow

Run the separate starter project:

```bash
TEMPORAL_CONFIG_FILE="$PWD/temporal.toml" \
TEMPORAL_LAMBDA_WORKFLOW_ID_PREFIX="$WORKFLOW_ID_PREFIX" \
  dotnet run \
  --project Starter \
  -- workflow
```

A successful run prints the Workflow ID, Run ID, and:

```text
Workflow result: Hello, Serverless Lambda Worker!!
```

## 8. Verify telemetry

Check that the Lambda and collector initialized without configuration errors:

```bash
aws logs tail "/aws/lambda/${FUNCTION_NAME}" --since 10m
```

The log should include the ADOT collector readiness message. The same invocation
should produce Temporal traces in X-Ray and `TemporalWorkerMetrics` metrics in
CloudWatch.

## 9. Clean up a dry run

First reset Temporal routing, then remove the Lambda-backed version and
deployment:

```bash
temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment set-current-version \
  --deployment-name "$DEPLOYMENT_NAME" \
  --unversioned \
  --allow-no-pollers \
  --yes

temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment delete-version \
  --deployment-name "$DEPLOYMENT_NAME" \
  --build-id "$BUILD_ID" \
  --skip-drainage

temporal --config-file "$PWD/temporal.toml" --profile default \
  worker deployment delete \
  --name "$DEPLOYMENT_NAME"

aws lambda delete-function --function-name "$FUNCTION_NAME"
```

Delete the AWS invocation stack, execution role, and retained log group:

```bash
aws cloudformation delete-stack --stack-name "$STACK_NAME"
aws cloudformation wait stack-delete-complete --stack-name "$STACK_NAME"

aws iam delete-role-policy \
  --role-name "$EXECUTION_ROLE_NAME" \
  --policy-name ADOT-Telemetry-Permissions
aws iam detach-role-policy \
  --role-name "$EXECUTION_ROLE_NAME" \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
aws iam delete-role --role-name "$EXECUTION_ROLE_NAME"

aws logs delete-log-group --log-group-name "/aws/lambda/${FUNCTION_NAME}"
```

Completed Workflow histories remain in Temporal Cloud unless explicitly
deleted. Use the Workflow and Run IDs printed by the starter if the dry-run
history must also be removed:

```bash
temporal --config-file "$PWD/temporal.toml" --profile default \
  workflow delete \
  --workflow-id "<workflow-id>" \
  --run-id "<run-id>"
```
