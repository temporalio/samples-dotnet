# External Storage

> [!WARNING]
> External storage is experimental and may change in the future.

This sample offloads large payloads to S3-compatible object storage with the SDK's external storage
support and the `Temporalio.Extensions.Aws.S3Driver.AwsSdk` package. A zlib payload codec
([ZlibCodec.cs](ZlibCodec.cs)) compresses every payload first, so what is stored inline in Temporal
and in S3 is compressed.

**Scenario:** The workflow receives an order count and calls a `FetchOrders` activity that returns
that many orders. The list, about 400 KiB even after compression, is passed to a second
`SummarizeOrders` activity, and the workflow returns its small `BatchSummary`.

The codec compresses each payload, and the SDK then compares the compressed size against the default
256 KiB threshold. Payloads still above it are stored in S3 and replaced in history by a small
reference. The workflow's own input and result are a few bytes and stay inline.

A mock S3 server ([S3Mock.cs](S3Mock.cs)) is included, so the sample runs locally without an AWS
account or Docker. To use real S3 instead, see [Using real S3](#using-real-s3).

### Running this sample

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this
directory in a separate terminal to start the mock S3 server. It listens on `localhost:9100`, serves
the `temporal-payloads` bucket, and keeps objects in memory until it stops.

    dotnet run s3-mock

In another terminal, start the worker:

    dotnet run worker

Then in another terminal, run the workflow:

    dotnet run workflow

Example output:

```text
Started workflow, run ID: <run-id>
Processed 200 orders weighing 5000.0 kg in total
```

The mock S3 server logs each request the driver makes:

```text
HEAD /temporal-payloads/v0/ns/default/wt/ProcessOrderBatchWorkflow/wi/external-storage-workflow-id/ri/<run-id>/d/sha256/<hash> status=NoSuchKey
PUT /temporal-payloads/v0/ns/default/wt/ProcessOrderBatchWorkflow/wi/external-storage-workflow-id/ri/<run-id>/d/sha256/<hash> status=200
GET /temporal-payloads/v0/ns/default/wt/ProcessOrderBatchWorkflow/wi/external-storage-workflow-id/ri/<run-id>/d/sha256/<hash> status=200
HEAD /temporal-payloads/v0/ns/default/wt/ProcessOrderBatchWorkflow/wi/external-storage-workflow-id/ri/<run-id>/d/sha256/<hash> status=200
GET /temporal-payloads/v0/ns/default/wt/ProcessOrderBatchWorkflow/wi/external-storage-workflow-id/ri/<run-id>/d/sha256/<hash> status=200
```

The worker stores the `FetchOrders` result (`HEAD` finds no object, `PUT` uploads it), and the
workflow retrieves it. When the workflow passes the same list to `SummarizeOrders`, the driver finds
the object already stored and skips the upload, and the activity retrieves it.

Now, with [Temporal CLI](https://github.com/temporalio/cli) on the `PATH`, show how the payloads
were stored:

    temporal workflow show --workflow-id external-storage-workflow-id

| Payload                  | Type                              | Storage           |
| ------------------------ | --------------------------------- | ----------------- |
| Workflow input           | `int`                             | Inline            |
| `FetchOrders` input      | `int`                             | Inline            |
| `FetchOrders` result     | `IReadOnlyList<Order>` (~400 KiB) | **External (S3)** |
| `SummarizeOrders` input  | `IReadOnlyList<Order>` (~400 KiB) | **External (S3)** |
| `SummarizeOrders` result | `BatchSummary`                    | Inline            |
| Workflow result          | `BatchSummary`                    | Inline            |

The two offloaded payloads are the order list passed from one activity to the next, which is what
external storage is for: large data that flows between activities without bloating workflow history.

### How it works

[Storage.cs](Storage.cs) builds the `DataConverter` that both the worker and the starter set on their
client. The worker uses its client's data converter, so it needs no other configuration.

```csharp
var driver = new S3StorageDriver(
    new AwsSdkS3StorageDriverClient(s3Client), bucket);
var dataConverter = DataConverter.Default with
{
    PayloadCodec = new ZlibCodec(),
    ExternalStorage = new(driver),
};
```

When encoding a payload, the SDK:

1. Serializes the value to a `Payload`.
2. Runs the codec, which compresses the payload.
3. Compares the compressed size against the threshold (default 256 KiB, set with
   `ExternalStorage.PayloadSizeThreshold`).
4. If it is at or above the threshold, stores the compressed payload in S3 and replaces it with a
   reference.

Decoding reverses these steps, retrieving from S3 and then decompressing.

Every process that reads or writes this workflow's payloads must use the same codec and external
storage configuration, so each can read what the others wrote.

#### Using real S3

Set `EXTERNAL_STORAGE_S3_BUCKET` to your bucket's name in the worker's and the workflow's terminals,
and skip the mock S3 server:

    export EXTERNAL_STORAGE_S3_BUCKET=<your-bucket>

The S3 client then takes its region and credentials from the usual AWS configuration, such as the
`AWS_REGION` and `AWS_PROFILE` environment variables or `~/.aws`. Set the variable in both
terminals: a worker and a starter that disagree would write payloads where the other can't read
them. See the
[S3 driver README](https://github.com/temporalio/sdk-dotnet/tree/main/src/Temporalio.Extensions.Aws.S3Driver)
for the IAM permissions the driver needs.

#### Why a codec

External storage works without a codec. Remove `ZlibCodec` and the order list is still offloaded,
only uncompressed. The sample uses one because many applications already run a codec, and the two
interact:

- The threshold applies to the encoded payload. Compression alone can keep a payload small enough to
  stay inline, so the threshold is a limit on the compressed size, not the size of your data.
- The driver stores the encoded payload. With an encryption codec in place of `ZlibCodec`, the
  objects in S3 are encrypted too.
- The reference that replaces an offloaded payload in history is not passed through the codec, so
  the bucket and key it names are visible to anyone who can read the history.
