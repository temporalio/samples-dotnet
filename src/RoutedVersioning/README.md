# Routed Versioning

This sample demonstrates how to version Workflows by having callers route to specific implementations, avoiding `Workflow.Patched()` conditionals.

> Q: How can I evolve my Workflow without `Workflow.Patched()` and its complex conditionals?

A: The recommended approach is to use [Worker Versioning](../WorkerVersioning). However, if that's unavailable, routed versioning lets callers choose which Workflow version to run, delegating internally to the appropriate implementation.

## How It Works

With routed versioning, **the caller decides which implementation version to run** by passing a version parameter. This is different from `Workflow.Patched()` where the routing logic lives inside the Workflow code.

The Worker registers a single facade Workflow that routes incoming requests to the appropriate implementation version based on the caller's choice.

## Evolving Your Workflow

When you need to create a new Workflow version:

1. Copy the current [Latest implementation](MyWorkflowLatest.workflow.cs) to a new version file (e.g., `MyWorkflowV2.workflow.cs`)
2. Make your changes to the new **Latest** file only
3. Add the new version to [WorkflowVersion.cs](WorkflowVersion.cs) (e.g., "v3")
4. Update the `versions` mapping in the [facade Workflow](MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors.workflow.cs) to route the new version
5. Deploy - no Worker registration changes needed
6. Callers then request the new version by passing it in their `StartMyWorkflowRequest.Options.Version`

This approach preserves your `git diff` experience since version changes are isolated to new files and the facade mapping.

## Running the Sample

### Start the Worker

```bash
dotnet run -- worker
```

The worker registers the facade Workflow and listens on task queue `rv`. It will display the current latest Workflow version on startup.

### Start a Workflow

In another terminal:

```bash
dotnet run -- starter --start-workflow my-workflow-1
```

This starts a Workflow using the current latest version. The Workflow ID is `my-workflow-1`.

### Query a Workflow

```bash
dotnet run -- starter --query-workflow my-workflow-1
```

This queries the Workflow for its result.

