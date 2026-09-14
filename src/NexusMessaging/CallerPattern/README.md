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

This sample requires a Temporal dev server build that supports Workflow Update callbacks. Download
the compatible binary from the [Temporal CLI pre-release instructions](https://docs.temporal.io/standalone-nexus-operation#temporal-cli-support).

Start the Temporal dev server with the required namespaces pre-created and Workflow Update
callbacks enabled:

```bash
./temporal server start-dev \
  --dynamic-config-value history.enableUpdateCallbacks=true \
  --dynamic-config-value history.enableCHASMSignalBacklinks=true \
  --namespace nexus-messaging-handler-namespace \
  --namespace nexus-messaging-caller-namespace
```

Create the Nexus endpoint:

```bash
./temporal operator nexus endpoint create \
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

Expected output:

```
Supported languages: Chinese, English
Current language: English
Set language from English to Chinese
Approved workflow
```
