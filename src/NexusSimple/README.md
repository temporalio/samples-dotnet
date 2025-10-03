# Nexus Simple

This sample demonstrates how to use Temporal for authoring a Nexus service and call it from a workflow.

### Instructions

To run, first see [README.md](../../README.md) for prerequisites such as starting the Temporal server.

Run the following to create both namespaces and an endpoint:

```
temporal operator namespace create --namespace nexus-simple-handler-namespace
temporal operator namespace create --namespace nexus-simple-caller-namespace

temporal operator nexus endpoint create \
  --name nexus-simple-endpoint \
  --target-namespace nexus-simple-handler-namespace \
  --target-task-queue nexus-simple-handler-sample \
  --description-file endpoint_description.md
```

In one terminal, run the handler worker from this directory:

```
dotnet run handler-worker
```

In a second terminal, run the caller worker from this directory:

```
dotnet run caller-worker
```

In a third terminal, run the caller workflow:

```
dotnet run caller-workflow
```