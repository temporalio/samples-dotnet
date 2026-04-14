## Entity pattern

The handler worker starts a `GreetingWorkflow` for a user ID at boot time.
`NexusGreetingService` routes every Nexus operation to that existing workflow by deriving
the workflow ID from the caller-supplied `UserId` (see the `WorkflowIdForUser` call).
The caller passes a `UserId`, not a workflow ID -- the handler is responsible for the
ID mapping.

The caller workflow:
1. Queries for supported languages (`GetLanguages` -- backed by a workflow query)
2. Queries the current language (`GetLanguage` -- backed by a workflow query)
3. Changes the language to Chinese (`SetLanguage` -- backed by a workflow update that calls an activity)
4. Approves the workflow (`Approve` -- backed by a workflow signal)

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
dotnet run --project src/NexusMessaging -- handler-worker
```

In a second terminal, start the caller worker:

```bash
dotnet run --project src/NexusMessaging -- caller-worker
```

In a third terminal, start the caller workflow:

```bash
dotnet run --project src/NexusMessaging -- caller-workflow
```
