# Nexus serialization context

This sample uses `ISerializationContext.Nexus` to select an encryption key for each Nexus
endpoint. The caller invokes synchronous and workflow-backed asynchronous operations
through four endpoints. The `compressed-handler-worker` compresses and then encrypts
Nexus payloads. The `encrypted-handler-worker` encrypts without compression. Each handler
selects between its own key 1 and key 2 using the endpoint in the Nexus context.
The caller and each handler run in separate namespaces.

| Handler worker | Endpoint | Key ID | Nexus payload encoding |
|---|---|---|---|
| Compressed/encrypted | `nexus-serialization-compressed-encrypted-1` | `compressed-encrypted-1` | zlib, then AES-GCM |
| Compressed/encrypted | `nexus-serialization-compressed-encrypted-2` | `compressed-encrypted-2` | zlib, then AES-GCM |
| Encrypted | `nexus-serialization-encrypted-1` | `encrypted-1` | AES-GCM |
| Encrypted | `nexus-serialization-encrypted-2` | `encrypted-2` | AES-GCM |

Each endpoint uses a different 128-bit AES key. Compression runs before encryption,
and decoding decrypts before decompressing. AES-GCM authenticates the ciphertext.
The AES-GCM and zlib codecs are implemented in this sample.
Nexus supplies the endpoint context used to select a codec. The context also identifies
the service and operation, which can be used to select keys.

## Adapt this pattern

The sample's [`NexusCodec`](NexusCodec.cs) selects encryption and compression settings
from an immutable endpoint map, with a default for ordinary workflow payloads. Set
`DataConverter.PayloadCodec` to a `NexusCodec` instance, then pass the data converter to
the caller and handler clients through `TemporalClientConnectOptions.DataConverter`.
See [SampleConfig.cs](SampleConfig.cs) for the key maps and [Program.cs](Program.cs)
for client setup.

Because AES-GCM uses symmetric encryption, the caller and handler share a secret key
for each endpoint. This sample uses that key for both requests and results.
Both sides also need the same payload format, including whether to compress.

The caller uses a separate key for its workflow input and final result.
For this example, each handler reuses its endpoint-1 key for backing workflow inputs.

When adapting this sample, use your existing codec for ordinary workflow payloads
so workers can still read existing histories. The sample selects the workflow default
in the `endpoint == null` case of `Selected` in [NexusCodec.cs](NexusCodec.cs).

| Ordinary workflow payload | Default key ID |
|---|---|
| Caller workflow input and final result | `caller-workflow` |
| Compressed/encrypted handler's backing workflow inputs | `compressed-encrypted-1` |
| Encrypted handler's backing workflow inputs | `encrypted-1` |

These workflow payloads use AES-GCM without compression or endpoint metadata.

On decode, the AES-GCM codec passes other encodings through for ordinary decoding.
For AES-GCM payloads, it checks the key ID against the codec selected by context and
verifies the ciphertext before returning the decoded value.

The SDK supplies Nexus context for operation requests and results. For workflow-backed
async operations, the server stores the context in workflow history and the SDK uses it
to select the result's key and compression. To see the two keys in one execution, call
`nexus-serialization-encrypted-2` and check the backing workflow's history. Its input
records `encrypted-1` and its result records `encrypted-2`.

The caller starts all eight operations before waiting for their results. Each operation
keeps its own endpoint context while calls using different keys run concurrently.
[`NexusCodec.WithSerializationContext`](NexusCodec.cs) binds the endpoint on a new
instance that shares the read-only configuration.
No custom interceptor or workflow-header propagation is required.

Each workflow execution has one serialized result, shared by all Nexus operations
attached to it. Operations that need results encrypted with different keys must use
separate workflow executions. This sample starts one backing workflow per async
operation, using the Nexus request ID as its workflow ID suffix. Retrying the same
request reuses that workflow ID.

The outer encrypted payload includes `encoding`, `encryption-key-id`, and, for Nexus
payloads, `nexus-endpoint-name` metadata. For example:

```text
encoding: binary/nexus-aes-gcm
encryption-key-id: compressed-encrypted-1
nexus-endpoint-name: nexus-serialization-compressed-encrypted-1
```

The metadata records the key ID and endpoint, while the key material stays in the
codec configuration.

The AES-GCM codec and hard-coded keys are for this example. For production, use an
established encryption implementation and secure key storage.

## Prerequisites

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download). This repository
uses Temporal .NET SDK 1.22.0.
Use a Temporal Server that supports Nexus serialization-context propagation for
workflow-backed asynchronous operations. For the local walkthrough, download the prerelease
[Temporal CLI `v1.9.3-nexus-serialization-context`](https://github.com/temporalio/cli/releases/tag/v1.9.3-nexus-serialization-context).
Extract the archive and place the `temporal` binary at the repository root.
The commands below use `./temporal` to run this build separately from your installed CLI.

## Run locally

Run these commands from the repository root. The CLI version should be
`1.9.3-nexus-serialization-context`:

```bash
./temporal --version
```

Each worker polls a task queue with the same name as its namespace. Both endpoints
for a handler target that handler's namespace and task queue.

| Worker | Namespace and task queue |
|---|---|
| Caller | `nexus-serialization-caller` |
| Compressed/encrypted handler | `nexus-serialization-compressed-encrypted-handler` |
| Encrypted handler | `nexus-serialization-encrypted-handler` |

Start the dev server in its own terminal:

```bash
./temporal server start-dev \
  --namespace nexus-serialization-caller \
  --namespace nexus-serialization-compressed-encrypted-handler \
  --namespace nexus-serialization-encrypted-handler
```

In another terminal, create two endpoints for each handler worker:

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

Start the caller workflow in another terminal:

```bash
dotnet run --project src/NexusSerializationContext -- caller-workflow
```

### Output

Expected output:

```text
nexus-serialization-compressed-encrypted-1: sync=Hello from Nexus; async=Hello from Nexus
nexus-serialization-compressed-encrypted-2: sync=Hello from Nexus; async=Hello from Nexus
nexus-serialization-encrypted-1: sync=Hello from Nexus; async=Hello from Nexus
nexus-serialization-encrypted-2: sync=Hello from Nexus; async=Hello from Nexus
```

All eight results should be `Hello from Nexus`.

The run creates five workflow executions: one caller and four async backing workflows.
The four sync operations return directly from their handlers.

## Test

Run the codec and workflow tests from the repository root:

```bash
dotnet test tests --filter FullyQualifiedName~NexusSerializationContext
```

The SDK reuses the pinned prerelease CLI from its cache or downloads it if needed.
The workflow test starts its own isolated server and checks the sync and async
results for all four endpoints.
