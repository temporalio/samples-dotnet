# Nexus Operation Backed by a Standalone Activity

> [!WARNING]
> Standalone Nexus Operations are in pre-release and may be subject to backwards-incompatible changes.
> They require a server version that supports this feature. Use the dev server build at:
> https://github.com/temporalio/cli/releases/tag/v1.7.4-standalone-nexus-operations.

This sample shows how to implement a Nexus Operation whose backing execution is a **Standalone
Activity**. `TemporalOperationHandler` maps the Temporal execution onto the Nexus Operation:
starting the operation starts the Activity, and when the Activity finishes Temporal delivers its
result to the Nexus caller.

### Sample structure

| File | Purpose                                                                                                          |
|---|------------------------------------------------------------------------------------------------------------------|
| [`IGreetingService.cs`](IGreetingService.cs) | Nexus service definition shared by caller and handler                                                            |
| [`Handler/GreetingActivities.cs`](Handler/GreetingActivities.cs) | The Standalone Activity backing the operation                                                                    |
| [`Handler/GreetingService.cs`](Handler/GreetingService.cs) | Operation implementation, via `TemporalOperationHandler.FromHandleFactory` and `StartActivityAsync`              |
| [`Program.cs`](Program.cs) | Worker hosting the Nexus handler and the Activity, plus the starter that executes the operation from client code |

The starter and worker connect to two different namespaces (a "caller" namespace and a "handler"
namespace) — this mirrors how Nexus is typically used to cross namespace boundaries. The client is
configured via the SDK's [environment configuration](https://docs.temporal.io/develop/environment-configuration)
support (`ClientEnvConfig.LoadClientConnectOptions()`), which reads `TEMPORAL_NAMESPACE`,
`TEMPORAL_ADDRESS`, etc. from the environment (and optionally profiles from `temporal.toml`).

## Run locally against a dev server

1. Start the [Temporal dev server build that supports standalone Nexus operations](https://docs.temporal.io/standalone-nexus-operation#temporal-cli-support)
   with the required namespaces pre-created and Activity callbacks enabled:

   ```bash
   ./temporal server start-dev \
     --dynamic-config-value activity.enableCallbacks=true \
     --namespace my-caller-namespace \
     --namespace my-handler-namespace
   ```

2. Create a Nexus endpoint that routes to the handler namespace and the worker's task queue:

   ```bash
   ./temporal operator nexus endpoint create \
     --name my-nexus-endpoint \
     --target-namespace my-handler-namespace \
     --target-task-queue nexus-handler-queue
   ```

3. In a second terminal, start the handler worker in the handler namespace:

   ```bash
   TEMPORAL_NAMESPACE=my-handler-namespace dotnet run worker
   ```

   You should see a log line that the worker has started on the `nexus-handler-queue` task queue.

4. In a third terminal, run the starter in the caller namespace:

   ```bash
   TEMPORAL_NAMESPACE=my-caller-namespace dotnet run starter
   ```

   You should see something similar to the following output:

   ```
   [09:00:30] Started Greet operation OperationID greeting-4d4b1f1e-0f2f-4f3e-9b0f-6b0b9a1c2d3e
   [09:00:30] Greet result: Hello, World!
   ```
