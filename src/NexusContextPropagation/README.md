# Nexus Context Propagation

This sample demonstrates how to propagate context through client calls, workflows, and Nexus headers. This is similar to
the [ContextPropagation](../ContextPropagation/) sample, but across namespaces with Nexus.

### Instructions

To run, first see [README.md](../../README.md) for prerequisites such as starting the Temporal server.

Run the following to create both namespaces and an endpoint:

```
temporal operator namespace create --namespace nexus-context-propagation-handler-namespace
temporal operator namespace create --namespace nexus-context-propagation-caller-namespace

temporal operator nexus endpoint create \
  --name nexus-context-propagation-endpoint \
  --target-namespace nexus-context-propagation-handler-namespace \
  --target-task-queue nexus-context-propagation-handler-sample
```

In one terminal, run the handler worker from this directory:

```
dotnet run handler-worker
```

In another terminal, run the caller worker from this directory:

```
dotnet run caller-worker
```

In another terminal, run the caller workflow:

```
dotnet run caller-workflow
```