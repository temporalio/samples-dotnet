# Worker-Specific Task Queues

Use a unique Task Queue for each Worker in order to have certain Activities run on a specific Worker.

This is useful in scenarios where multiple Activities need to run in the same process or on the same host, for example to share memory or disk. This sample has a file processing Workflow, where one Activity downloads the file to disk and other Activities process it and clean it up.

This strategy is:

- Each Worker process runs two `TemporalWorker`s:
  - One `TemporalWorker` listens on the shared `worker-specific-task-queues-sample` Task Queue.
  - Another `TemporalWorker` listens on a uniquely generated Task Queue.
- Create a `GetUniqueTaskQueue` Activity that returns one of the uniquely generated Task Queues (that only one Worker is listening on—i.e. the **Worker-specific Task Queue**). It doesn't matter where this Activity is run, so it can be executed on the shared Task Queue. In this sample, the unique Task Queue is simply a `uuid`, but you can inject smart logic here to uniquely identify the Worker, [as Netflix did](https://community.temporal.io/t/using-dynamic-task-queues-for-traffic-routing/3045).
- The Workflow and the first Activity are run on the shared `worker-specific-task-queues-sample` Task Queue. The rest of the Activities that do the file processing are run on the Worker-specific Task Queue.

Activities have been artificially slowed with `await Task.Delay(TimeSpan.FromSeconds(3))` to simulate slow activities.

### Running this sample

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

In the worker terminal, you should see logs like:

```
Running worker
[21:57:26] info: Temporalio.Activity:DownloadFileToWorkerFileSystem[0]
      Downloading https://temporal.io and saving to path /tmp/tmpD5c6wy.tmp
[21:57:32] info: Temporalio.Activity:WorkOnFileInWorkerFileSystem[0]
      Did some work on /tmp/tmpD5c6wy.tmp, checksum: 49d7419e6cba3575b3158f62d053f922aa08b23c64f05411cda3213b56c84ba4
[21:57:35] info: Temporalio.Activity:CleanupFileFromWorkerFileSystem[0]
      Removing /tmp/tmpD5c6wy.tmp
```