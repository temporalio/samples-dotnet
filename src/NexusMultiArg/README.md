# Nexus Context Propagation

This sample demonstrates how to convert a single Nexus argument into multiple workflow arguments.

### Instructions

To run, first see [README.md](../../README.md) for prerequisites such as starting the Temporal server.

Run the following to create both namespaces and an endpoint:

```
temporal operator namespace create --namespace nexus-multi-arg-handler-namespace
temporal operator namespace create --namespace nexus-multi-arg-caller-namespace

temporal operator nexus endpoint create \
  --name nexus-multi-arg-endpoint \
  --target-namespace nexus-multi-arg-handler-namespace \
  --target-task-queue nexus-multi-arg-handler-sample
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