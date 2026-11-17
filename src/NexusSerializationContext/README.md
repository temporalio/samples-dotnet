# Nexus serialization context

This sample uses `ISerializationContext.Nexus` to select an encryption key for each Nexus
endpoint. The caller invokes synchronous and workflow-backed asynchronous operations
through four endpoints. The `compressed-handler-worker` command compresses and then
encrypts Nexus payloads. The `encrypted-handler-worker` command encrypts without
compression. Each handler selects between its own key 1 and key 2 using
the endpoint in the Nexus context.
The caller and each handler run in separate namespaces.

| Handler | Endpoint | Encryption key ID | Nexus payload encoding |
|---|---|---|---|
| `compressed-handler-worker` | `nexus-serialization-compressed-encrypted-1` | `compressed-encrypted-1` | zlib, then AES-GCM |
| `compressed-handler-worker` | `nexus-serialization-compressed-encrypted-2` | `compressed-encrypted-2` | zlib, then AES-GCM |
| `encrypted-handler-worker` | `nexus-serialization-encrypted-1` | `encrypted-1` | AES-GCM |
| `encrypted-handler-worker` | `nexus-serialization-encrypted-2` | `encrypted-2` | AES-GCM |

These four endpoint keys are distinct 128-bit AES keys. Compression runs before encryption;
decoding decrypts before decompressing. AES-GCM authenticates the ciphertext.
The AES-GCM and zlib envelope handling is implemented in `NexusCodec`; Nexus supplies the endpoint
context used to select them.

Each worker configures `NexusCodec` with an immutable endpoint configuration map and default encryption
for ordinary workflow payloads. The caller uses a separate key, `caller-workflow`, for
its own input and final result, keeping those payloads independent of the keys shared
with Nexus handlers. Each handler reuses its key 1 for backing workflow inputs.

| Ordinary workflow payload | Encryption key ID |
|---|---|
| Caller workflow input and final result | `caller-workflow` |
| Compressed/encrypted handler workflow inputs | `compressed-encrypted-1` |
| Encrypted handler workflow inputs | `encrypted-1` |

These workflow payloads use AES-GCM without compression or endpoint metadata.
An async Nexus result uses the key and compression chain selected by its endpoint,
even when that differs from the backing workflow's input encryption.

For synchronous operations, the SDK supplies Nexus context when decoding inputs and
encoding results. When an asynchronous handler starts `EchoWorkflow`, the SDK includes
the Nexus endpoint, service, and operation in the workflow-start request. The server
stores these fields in the `WorkflowExecutionStarted` history event as
`propagated_nexus_serialization_context`. The worker SDK restores the Nexus context from that event when encoding the workflow's result, so
`NexusCodec` can select the endpoint's codec and key from the worker's local configuration.
The workflow's input still uses ordinary workflow context; this sample's default codec
encrypts it with that handler's key 1.

The caller starts all eight operations before waiting for their results. Each operation
keeps its own endpoint context, even when operations for different keys are in flight.
No custom interceptor or workflow-header propagation is required.

The outer encrypted payload includes `encoding`, `encryption-key-id`, and, for Nexus
payloads, `nexus-endpoint-name` metadata. For example:

```text
encoding: binary/nexus-aes-gcm
encryption-key-id: compressed-encrypted-1
nexus-endpoint-name: nexus-serialization-compressed-encrypted-1
```

A Codec Server can use that metadata to select the key and decompression chain without
SDK context. Ordinary workflow payloads have a key ID but no endpoint metadata or compression.

The AES-GCM codec and hard-coded keys are for this example. Use an established encryption
implementation and secure key storage for production.

## Prerequisites

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
Use a Temporal Server that supports Nexus serialization-context propagation for
workflow-backed asynchronous operations. For local development, download
[Temporal CLI `v1.9.3-nexus-serialization-context`](https://github.com/temporalio/cli/releases/tag/v1.9.3-nexus-serialization-context).
Keep this prerelease binary separate from your stable CLI. Place the downloaded binary
at the repository root as `temporal`; the commands below use `./temporal` explicitly.

## Run locally

Run these commands from the repository root. Each worker polls a task queue with the
same name as its namespace. Both endpoints for a handler target that handler's namespace
and task queue.

Start the dev server in a terminal:

```bash
./temporal server start-dev \
  --namespace nexus-serialization-caller \
  --namespace nexus-serialization-compressed-encrypted-handler \
  --namespace nexus-serialization-encrypted-handler
```

Create two endpoints for each handler:

```bash
./temporal operator nexus endpoint create \
  --name nexus-serialization-compressed-encrypted-1 \
  --target-namespace nexus-serialization-compressed-encrypted-handler \
  --target-task-queue nexus-serialization-compressed-encrypted-handler
./temporal operator nexus endpoint create \
  --name nexus-serialization-compressed-encrypted-2 \
  --target-namespace nexus-serialization-compressed-encrypted-handler \
  --target-task-queue nexus-serialization-compressed-encrypted-handler
./temporal operator nexus endpoint create \
  --name nexus-serialization-encrypted-1 \
  --target-namespace nexus-serialization-encrypted-handler \
  --target-task-queue nexus-serialization-encrypted-handler
./temporal operator nexus endpoint create \
  --name nexus-serialization-encrypted-2 \
  --target-namespace nexus-serialization-encrypted-handler \
  --target-task-queue nexus-serialization-encrypted-handler
```

Run each worker in its own terminal:

```bash
dotnet run --project src/NexusSerializationContext -- compressed-handler-worker
```

```bash
dotnet run --project src/NexusSerializationContext -- encrypted-handler-worker
```

```bash
dotnet run --project src/NexusSerializationContext -- caller-worker
```

Run the caller workflow:

```bash
dotnet run --project src/NexusSerializationContext -- caller-workflow
```

The starter prints four paired results:

```text
nexus-serialization-compressed-encrypted-1: sync=Hello from Nexus; async=Hello from Nexus
nexus-serialization-compressed-encrypted-2: sync=Hello from Nexus; async=Hello from Nexus
nexus-serialization-encrypted-1: sync=Hello from Nexus; async=Hello from Nexus
nexus-serialization-encrypted-2: sync=Hello from Nexus; async=Hello from Nexus
```

One caller execution starts four backing `EchoWorkflow` executions, one per async
operation. Synchronous operations return directly. Backing workflow IDs use the Nexus
request ID, which stays stable across retries and differs across independent operations.
Rerunning the starter creates a new caller execution and four new backing executions.

## Test

Run the codec and workflow tests from the repository root:

```bash
TEMPORAL_SERIALIZATION_CONTEXT_CLI="$PWD/temporal" \
  dotnet test tests --filter FullyQualifiedName~NexusSerializationContext
```

The workflow test starts its own isolated server, checks all eight results, and inspects
history for stored async context and the distinct input/result encryption keys.
