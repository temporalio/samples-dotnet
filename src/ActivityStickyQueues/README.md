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

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.