#!/bin/bash
set -euo pipefail

FUNCTION_NAME="${1:?Usage: deploy-lambda.sh <function-name> [execution-role-arn]}"
EXECUTION_ROLE_ARN="${2:-${LAMBDA_EXECUTION_ROLE_ARN:-}}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SAMPLE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/bin/lambda-publish"
ZIP_FILE="$SCRIPT_DIR/function.zip"
TARGET_RUNTIME="${TEMPORAL_DOTNET_LAMBDA_RUNTIME:-linux-x64}"

cleanup() {
  rm -rf "$PUBLISH_DIR" "$ZIP_FILE"
}
trap cleanup EXIT

case "$TARGET_RUNTIME" in
  linux-x64)
    ARCHITECTURE="x86_64"
    ;;
  linux-arm64)
    ARCHITECTURE="arm64"
    ;;
  *)
    echo "Unsupported TEMPORAL_DOTNET_LAMBDA_RUNTIME: $TARGET_RUNTIME" >&2
    echo "Use linux-x64 or linux-arm64." >&2
    exit 1
    ;;
esac

rm -rf "$PUBLISH_DIR" "$ZIP_FILE"
dotnet publish "$SAMPLE_DIR/Worker/TemporalioSamples.LambdaWorker.Worker.csproj" \
  --configuration Release \
  --runtime "$TARGET_RUNTIME" \
  --self-contained false \
  --output "$PUBLISH_DIR"

if [[ ! -f "$PUBLISH_DIR/libtemporalio_sdk_core_c_bridge.so" ]]; then
  echo "Publish output is missing the $TARGET_RUNTIME Temporal native bridge." >&2
  exit 1
fi

cp "$SAMPLE_DIR/temporal.toml" "$SAMPLE_DIR/otel-collector-config.yaml" \
  "$PUBLISH_DIR/"

cd "$PUBLISH_DIR"
zip -r "$ZIP_FILE" .

if aws lambda get-function \
  --function-name "$FUNCTION_NAME" \
  --query 'Configuration.FunctionName' \
  --output text >/dev/null 2>&1; then
  aws lambda update-function-code \
    --function-name "$FUNCTION_NAME" \
    --zip-file fileb://"$ZIP_FILE" \
    --query '{FunctionName:FunctionName,CodeSha256:CodeSha256,LastModified:LastModified,RevisionId:RevisionId}' \
    --output json
  aws lambda wait function-updated --function-name "$FUNCTION_NAME"
else
  if [[ -z "$EXECUTION_ROLE_ARN" ]]; then
    echo "Lambda function $FUNCTION_NAME does not exist." >&2
    echo "Pass its execution-role ARN as the second argument to create it." >&2
    exit 1
  fi
  # A newly created IAM role can take a few seconds to become assumable by Lambda.
  for attempt in {1..12}; do
    if CREATE_OUTPUT="$(aws lambda create-function \
      --function-name "$FUNCTION_NAME" \
      --runtime dotnet8 \
      --handler TemporalioSamples.LambdaWorker.Worker::TemporalioSamples.LambdaWorker.Worker.LambdaFunction::HandlerAsync \
      --role "$EXECUTION_ROLE_ARN" \
      --architectures "$ARCHITECTURE" \
      --timeout 600 \
      --memory-size 256 \
      --zip-file fileb://"$ZIP_FILE" \
      --query '{FunctionName:FunctionName,FunctionArn:FunctionArn,Runtime:Runtime,Architectures:Architectures,State:State,RevisionId:RevisionId}' \
      --output json 2>&1)"; then
      printf '%s\n' "$CREATE_OUTPUT"
      break
    fi
    if [[ "$CREATE_OUTPUT" != *"cannot be assumed by Lambda"* || "$attempt" -eq 12 ]]; then
      printf '%s\n' "$CREATE_OUTPUT" >&2
      exit 1
    fi
    echo "Waiting for the execution role to propagate to Lambda..." >&2
    sleep 5
  done
  aws lambda wait function-active --function-name "$FUNCTION_NAME"
fi
