# WorkflowTypeVersioning


> Q. How can I avoid the use of `patch` and the complex conditionals it introduces into my Workflow Code?

A. The BEST option is to use [WorkerVersioning](../WorkerVersioning).

But if you can't use WorkerVersioning, you can use `WorkflowType` versioning to evolve your Workflow code without use of `patch`.

## Approaches

### Copy/Paste

Say the following Workflow is deployed to production.

```c#
[Workflow]
class MyWorkflow {...}
```

Now you want to implement `V2` of your Workflow.

You keep the _old_ version (`MyWorkflow`) registered on Workers, but now you register the new implementation too:

```c#
[Workflow]
class MyWorkflowV2 {...}
```

Now your _callers_ start new Workflows with explicit `MyWorkflowV2` type name referenced, eg:

```c#
var handle = await client.StartWorkflowAsync((MyWorkflowV2 wf) => wf.RunAsync(args), new(id: workflowId, taskQueue: taskQueue));
```

This _works_! 
...But now the code review isn't so easy because this results in a _brand new file_ in the commit so doing a `git diff` is more complicated if you only
want to see the actual _changes_ to the implementation.

If you want to preserve your `git diff` experience AND benefit from Workflow Type versioning, a different approach is needed.

### Type Version Mapping

Instead of exposing the `[Workflow]` type directly to callers, consider changing out implementations on the "inside" of the Workflow that is registered with the Worker.
This makes your Workflow a kind of `facade` that delegates workflow methods to the _actual_ implementation mapped inside, similar to a **strategy** pattern.

The evolution of Workflow code then becomes:
1. Copy the current implementation file and name append the version name that is appropriate. For example, if you are working on "version 3" of `MyWorkflow`, you would copy the [Latest](MyWorkflowLatest.workflow.cs) version over to [V2](MyWorkflowV2.workflow.cs).
2. Make the changes to the **Latest** file _only_.
3. Add the new version to `WorkflowVersion` (eg "v3") and update the `versions` mapping inside the [facade](MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors.workflow.cs) to accept the new version.
4. Deploy the new code..no change is needed to the Worker registration!
5. Update your Callers to target the new `WorkflowVersion.V3` inside either input arguments, a memo, or however you want to resolve the type map.

