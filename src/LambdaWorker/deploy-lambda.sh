#!/bin/bash
set -euo pipefail

FUNCTION_NAME="${1:?Usage: deploy-lambda.sh <function-name>}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/bin/lambda-publish"
ZIP_FILE="$SCRIPT_DIR/function.zip"
TARGET_RUNTIME="${TEMPORAL_DOTNET_LAMBDA_RUNTIME:-linux-x64}"

case "$TARGET_RUNTIME" in
  linux-x64|linux-arm64)
    ;;
  *)
    echo "Unsupported TEMPORAL_DOTNET_LAMBDA_RUNTIME: $TARGET_RUNTIME" >&2
    echo "Use linux-x64 or linux-arm64." >&2
    exit 1
    ;;
esac

rm -rf "$PUBLISH_DIR" "$ZIP_FILE"
dotnet publish "$SCRIPT_DIR/TemporalioSamples.LambdaWorker.csproj" \
  --configuration Release \
  --runtime "$TARGET_RUNTIME" \
  --self-contained false \
  --output "$PUBLISH_DIR"

if [[ ! -f "$PUBLISH_DIR/libtemporalio_sdk_core_c_bridge.so" ]]; then
  echo "Publish output is missing the $TARGET_RUNTIME Temporal native bridge." >&2
  exit 1
fi

cp "$SCRIPT_DIR/temporal.toml" "$SCRIPT_DIR/otel-collector-config.yaml" \
  "$PUBLISH_DIR/"

cd "$PUBLISH_DIR"
zip -r "$ZIP_FILE" .

aws lambda update-function-code \
  --function-name "$FUNCTION_NAME" \
  --zip-file fileb://"$ZIP_FILE" \
  --query '{FunctionName:FunctionName,CodeSha256:CodeSha256,LastModified:LastModified,RevisionId:RevisionId}' \
  --output json

rm -rf "$PUBLISH_DIR" "$ZIP_FILE"
