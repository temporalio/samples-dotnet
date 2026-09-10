## On-demand pattern

No Workflow is pre-started. The caller creates and controls Workflow instances through Nexus
operations. `NexusRemoteGreetingService` adds a `RunFromRemote` operation that starts a
`GreetingWorkflow`, and every operation includes a `UserId` so the handler can derive
the target Workflow ID.

The caller Workflow:
1. Attaches approval context for the first user via `AttachApprovalContext`, before anything has
   started that user's Workflow
2. Starts two remote `GreetingWorkflow` instances via `RunFromRemote` (backed by a Workflow started
   through `ITemporalNexusClient.StartWorkflowAsync`)
3. Attaches approval context for the second user, whose Workflow now already exists
4. Workflow one: queries supported languages, changes to Spanish, and approves
5. Workflow two: queries the current language, changes to French, and approves
6. Waits for each to complete and returns their results

### Running

This sample requires a Temporal dev server build that supports Workflow Update callbacks. Download
the compatible binary from the [Temporal CLI pre-release instructions](https://docs.temporal.io/standalone-nexus-operation#temporal-cli-support).

Start the Temporal dev server with the required namespaces pre-created, and Workflow Update
callbacks and signal-with-start from a Workflow enabled:

```bash
./temporal server start-dev \
  --dynamic-config-value history.enableUpdateCallbacks=true \
  --dynamic-config-value history.enableCHASMSignalBacklinks=true \
  --dynamic-config-value history.enableSignalWithStartFromWorkflow=true \
  --namespace nexus-messaging-handler-namespace \
  --namespace nexus-messaging-caller-namespace
```

Create the Nexus endpoint:

```bash
./temporal operator nexus endpoint create \
  --name nexus-messaging-on-demand-pattern-endpoint \
  --target-namespace nexus-messaging-handler-namespace \
  --target-task-queue nexus-messaging-handler-sample
```

In one terminal, start the handler worker:

```bash
dotnet run --project src/NexusMessaging -- remote-handler-worker
```

In a second terminal, start the caller worker:

```bash
dotnet run --project src/NexusMessaging -- remote-caller-worker
```

In a third terminal, run the following command to start the example:

```bash
dotnet run --project src/NexusMessaging -- remote-caller-workflow
```

Expected output:

```
Attached approval context before the workflow existed: user-one
Started remote workflow for user: user-one
Started remote workflow for user: user-two
Attached approval context to the running workflow: user-two
[One] Supported languages: Chinese, English
[One] Set language from English to Spanish
[One] Approved
[Two] Current language: English
[Two] Set language from English to French
[Two] Approved
[One] Result: Hola, mundo (approved by CallerRemoteWorkflow)
[Two] Result: Bonjour, monde (approved by CallerRemoteWorkflow)
```
