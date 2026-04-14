## On-demand pattern

No workflow is pre-started. The caller creates and controls workflow instances through Nexus
operations. `NexusRemoteGreetingService` adds a `RunFromRemote` operation that starts a new
`GreetingWorkflow`, and every other operation includes a `UserId` so the handler can derive
the target workflow ID.

The caller workflow:
1. Starts two remote `GreetingWorkflow` instances via `RunFromRemote` (backed by `WorkflowRunOperationHandler`)
2. Workflow one: queries supported languages, changes to Spanish, and approves
3. Workflow two: queries the current language, changes to French, and approves
4. Waits for each to complete and returns their results

### Running

Start a Temporal server:

```bash
temporal server start-dev
```

Create the namespaces and Nexus endpoint:

```bash
temporal operator namespace create --namespace nexus-messaging-handler-namespace
temporal operator namespace create --namespace nexus-messaging-caller-namespace

temporal operator nexus endpoint create \
  --name nexus-messaging-nexus-endpoint \
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

In a third terminal, start the caller workflow:

```bash
dotnet run --project src/NexusMessaging -- remote-caller-workflow
```
