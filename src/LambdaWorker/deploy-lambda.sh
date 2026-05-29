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

BRIDGE_FILE="libtemporalio_sdk_core_c_bridge.so"
if [[ ! -f "$PUBLISH_DIR/$BRIDGE_FILE" ]]; then
  if [[ -n "${TEMPORAL_DOTNET_NATIVE_BRIDGE:-}" ]]; then
    cp "$TEMPORAL_DOTNET_NATIVE_BRIDGE" "$PUBLISH_DIR/$BRIDGE_FILE"
  elif [[ "$TARGET_RUNTIME" == "linux-x64" && -n "${TEMPORAL_DOTNET_LINUX_X64_BRIDGE:-}" ]]; then
    cp "$TEMPORAL_DOTNET_LINUX_X64_BRIDGE" "$PUBLISH_DIR/$BRIDGE_FILE"
  else
    NUGET_PACKAGES_ROOT="${NUGET_PACKAGES:-}"
    if [[ -z "$NUGET_PACKAGES_ROOT" ]]; then
      NUGET_PACKAGES_ROOT="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"
    fi
    BRIDGE_FROM_NUGET="$(
      find "$NUGET_PACKAGES_ROOT/temporalio" \
        -path "*/runtimes/$TARGET_RUNTIME/native/$BRIDGE_FILE" \
        -print 2>/dev/null | sort -V | tail -1
    )"
    if [[ -z "$BRIDGE_FROM_NUGET" ]]; then
      echo "Missing $BRIDGE_FILE in publish output." >&2
      echo "Set TEMPORAL_DOTNET_NATIVE_BRIDGE to a $TARGET_RUNTIME Temporal bridge library path." >&2
      exit 1
    fi
    cp "$BRIDGE_FROM_NUGET" "$PUBLISH_DIR/$BRIDGE_FILE"
  fi
fi

cp "$SCRIPT_DIR/temporal.toml" "$SCRIPT_DIR/otel-collector-config.yaml" \
  "$PUBLISH_DIR/"

cd "$PUBLISH_DIR"
zip -r "$ZIP_FILE" .

aws lambda update-function-code --function-name "$FUNCTION_NAME" --zip-file fileb://"$ZIP_FILE"

rm -rf "$PUBLISH_DIR" "$ZIP_FILE"
