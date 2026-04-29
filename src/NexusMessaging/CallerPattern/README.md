## Entity pattern

The handler worker starts a `GreetingWorkflow` for a User ID at boot time.
`NexusGreetingService` routes every Nexus operation to that existing Workflow by deriving
the Workflow ID from the caller-supplied `UserId` (see the `WorkflowIdForUser` call).
The caller passes a `UserId`, not a Workflow ID -- the handler is responsible for the
ID mapping.

The caller Workflow:
1. Queries for supported languages (`GetLanguages` -- backed by a Workflow query)
2. Queries the current language (`GetLanguage` -- backed by a Workflow query)
3. Changes the language to Chinese (`SetLanguage` -- backed by a Workflow update that calls an activity)
4. Approves the Workflow (`Approve` -- backed by a Workflow signal)

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
  --name nexus-messaging-caller-pattern-endpoint \
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

In a third terminal, run the following command to start the example:

```bash
dotnet run --project src/NexusMessaging -- caller-workflow
```
