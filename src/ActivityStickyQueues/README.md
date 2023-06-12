# Sticky Activity Queues

This sample shows how to have [Sticky Execution](https://docs.temporal.io/tasks/#sticky-execution): using a unique task queue per Worker to have certain activities only run on that specific Worker.

The strategy is:

- Create a `GetUniqueTaskQueue` activity that generates a unique task queue name, `uniqueWorkerTaskQueue`.
- It doesn't matter where this activity is run, so it can be "non sticky" as per Temporal default behavior.
- In this demo, `uniqueWorkerTaskQueue` is simply a `uuid` initialized in the Worker, but you can inject smart logic here to uniquely identify the Worker, [as Netflix did](https://community.temporal.io/t/using-dynamic-task-queues-for-traffic-routing/3045).
- For activities intended to be "sticky", only register them in one Worker, and have that be the only Worker listening on that `uniqueWorkerTaskQueue`.
- Execute workflows from the Client like normal.

Activities have been artificially slowed with `await Task.Delay(TimeSpan.FromSeconds(3))` to simulate slow activities.

### Running this sample

1. `temporal server start-dev` to start [Temporal Server](https://github.com/temporalio/cli/#installation).
2. `dotnet run worker` to start the Worker.
3. In another shell, `dotnet run workflow` to run the Workflow.

Example output:

```bash
Running worker
[19:17:58] info: Temporalio.Activity:DownloadFileToWorkerFileSystem[0]
      Downloading https://temporal.io and saving to path C:\Users\jaken\AppData\Local\Temp\tmp4409.tmp
[19:18:04] info: Temporalio.Activity:WorkOnFileInWorkerFileSystem[0]
      Did some work on C:\Users\jaken\AppData\Local\Temp\tmp4409.tmp, checksum: b3fc767460efa514753a75e6f3d7af97
[19:18:07] info: Temporalio.Activity:CleanupFileFromWorkerFileSystem[0]
      Removing C:\Users\jaken\AppData\Local\Temp\tmp4409.tmp
```
