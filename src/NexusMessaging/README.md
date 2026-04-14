# Nexus Messaging Sample

This sample demonstrates how to expose a long-running workflow's queries, updates, and signals as Nexus operations. It contains two sub-examples.

## callerpattern (Entity Pattern)

The handler worker pre-starts a `GreetingWorkflow` entity for each user at boot time. The Nexus service exposes 4 sync operations that interact with this long-running workflow:

- **GetLanguages** — queries the entity workflow for supported languages
- **GetLanguage** — queries the entity workflow for the current language
- **SetLanguage** — executes an update on the entity workflow (fetches activity for unknown languages)
- **Approve** — signals the entity workflow to complete

The `CallerWorkflow` takes a `userId`, calls all 4 operations via Nexus, and returns a string log.

## ondemandpattern (On-Demand Pattern)

No pre-started workflow. The caller creates greeting workflows on demand:

- **RunFromRemote** — a `WorkflowRunOperation` that starts a new `GreetingWorkflow`
- **GetLanguages / GetLanguage / SetLanguage / Approve** — same operations as above, but targeting a workflow by its ID

The `CallerRemoteWorkflow` starts two remote workflows ("one" and "two"), interacts with both, then waits for their results.

## Configuration

- Handler namespace: `nexus-messaging-handler-namespace`
- Caller namespace: `nexus-messaging-caller-namespace`
- Nexus endpoint: `nexus-messaging-nexus-endpoint`
- Handler task queue: `nexus-messaging-handler-task-queue`

## Running

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
  --target-task-queue nexus-messaging-handler-task-queue
```

For the caller pattern, start the handler worker in one terminal:

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

Expected output:
```bash
Executing caller workflow
Caller workflow result:
  Supported languages: Chinese, English
  Current language: English
  Set language from English to Chinese
  Approved workflow
```   

And for the On Demand pattern, start the handler worker in one terminal:

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

Expected output:
```bash
Executing remote caller workflow (on-demand pattern)
Remote caller workflow result:
Started remote workflow for user: user-one
Started remote workflow for user: user-two
[One] Supported languages: Chinese, English
[One] Set language from English to Spanish
[One] Approved
[Two] Current language: English
[Two] Set language from English to French
[Two] Approved
[One] Result: Hola, mundo (approved by CallerRemoteWorkflow)
[Two] Result: Bonjour, monde (approved by CallerRemoteWorkflow)
```
