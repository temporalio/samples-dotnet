# Nexus Standalone Operations

This sample demonstrates how to use Standalone Nexus Operations (executing Nexus operations directly
from client code without wrapping them in a Workflow). It shows both sync and async
(workflow-backed) operations, and how to use the `ListNexusOperationsAsync` and
`CountNexusOperationsAsync` APIs.

> [!NOTE]
> Standalone Nexus operations require a server version that supports this feature. Use the dev
> server build at:  
> https://github.com/temporalio/cli/releases/tag/v1.7.3-standalone-nexus-operations.

### Steps to run this sample

1. Run the [Temporal dev server build that supports standalone Nexus operations](https://github.com/temporalio/cli/releases/tag/v1.7.3-standalone-nexus-operations). (If you are going to run locally, you will want to start it in another terminal; this command is blocking and runs until it receives a SIGINT (Ctrl + C) command.)

   Start the dev server with the dynamic config flags required for standalone Nexus operations:

   ```bash
   temporal server start-dev \
     --dynamic-config-value "nexusoperation.enableStandalone=true" \
     --dynamic-config-value "history.enableChasmCallbacks=true"
   ```

   You should see a line about the CLI, Server and UI versions, and one line each for the Server
   URL, UI URL and Metrics endpoint. It should look something like this:

   ```
   Temporal CLI 1.7.3-standalone-nexus-operations (Server 1.32.0-158.0, UI 2.52.0)

   Temporal Server:  localhost:7233
   Temporal UI:      http://localhost:8233
   Temporal Metrics: http://localhost:61951/metrics
   ```

2. Create a Nexus endpoint that routes to the worker's task queue. In a second terminal, run:

   ```bash
   temporal operator nexus endpoint create \
     --name nexus-standalone-operations-endpoint \
     --target-namespace default \
     --target-task-queue nexus-standalone-operations
   ```

3. Then run the following command from this directory to start the worker. The worker is a
   blocking process that runs until it receives a SIGINT (Ctrl + C) command.

   ```bash
   dotnet run worker
   ```

   You should see a log line that the worker has started on the `nexus-standalone-operations` task
   queue.

4. In a third terminal, run the following command from this directory to start the example:

   ```bash
   dotnet run starter
   ```

   You should see something similar to the following output:

   ```
   [09:00:30] Started Echo operation OperationID nexus-standalone-echo-op
   [09:00:30] Echo result: hello
   [09:00:30] Started Hello operation OperationID nexus-standalone-hello-op
   [09:00:30] Hello result: Hello Temporal 👋
   [09:00:30] ListNexusOperations results:
   [09:00:30]     OperationID: nexus-standalone-hello-op, Operation: SayHello, Status: Completed
   [09:00:30]     OperationID: nexus-standalone-echo-op, Operation: Echo, Status: Completed
   [09:00:30] Total Nexus operations: 2
   ```

   If you run the starter multiple times, you should see additional `ListNexusOperations` results,
   as more operations are run. The same goes for the number from `CountNexusOperations`.
