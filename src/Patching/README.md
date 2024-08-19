# Patching Sample

This sample shows how to safely alter a workflow using `Workflow.Patched` and `Workflow.DeprecatePatch` in stages.

To run, first see [README.md](../../README.md) for prerequisites. Then follow the patching stages below.

### Stage 1 - Initial code

This stage is for existing running workflows. To simulate our initial workflow, run the worker in a separate terminal:

    dotnet run worker --workflow initial

Now we can start this workflow:

    dotnet run starter --start-workflow initial-workflow-id

This will output "Started workflow with ID initial-workflow-id and ...". Now query this workflow:

    dotnet run starter --query-workflow initial-workflow-id

This will output "Query result for ID initial-workflow-id: pre-patch".

### Stage 2 - Patch the workflow

This stage is for needing to run old and new workflows at the same time. To simulate our patched workflow, stop the
worker from before and start it again with the patched workflow:

    dotnet run worker --workflow patched

Now let's start another workflow with this patched code:

    dotnet run starter --start-workflow patched-workflow-id

This will output "Started workflow with ID patched-workflow-id and ...". Now query the old workflow that's still
running:

    dotnet run starter --query-workflow initial-workflow-id

This will output "Query result for ID initial-workflow-id: pre-patch" since it is pre-patch. But if we execute a query
against the new code:

    dotnet run starter --query-workflow patched-workflow-id

We get "Query result for ID patched-workflow-id: post-patch". This is how old workflow code can take old paths and new
workflow code can take new paths.

### Stage 3 - Deprecation

Once we know that all workflows that started with the initial code from "Stage 1" are no longer running, we don't need
the patch so we can deprecate it. To use the patch deprecated workflow, stop the workflow from before and start it again
with:

    dotnet run worker --workflow patch-deprecated

If we query a workflow in "Stage 1" we should get an error. This will output "Unhandled exception. 
Temporalio.Exceptions.WorkflowQueryFailedException: [TMPRL1100] Nondeterminism error: Activity type of scheduled event 
'PrePatchActivity' does not match activity type of activity command 'PostPatchActivity'"

    dotnet run starter --query-workflow initial-workflow-id

All workflows in "Stage 2" and any new workflows will work. Now let's start another workflow with this patch deprecated
code:

    dotnet run starter --start-workflow patch-deprecated-workflow-id

This will output "Started workflow with ID patch-deprecated-workflow-id and ...". Now query the patched workflow that's
still running:

    dotnet run starter --query-workflow patched-workflow-id

This will output "Query result for ID patched-workflow-id: post-patch". And if we execute a query against the latest
workflow:

    dotnet run starter --query-workflow patch-deprecated-workflow-id

As expected, this will output "Query result for ID patch-deprecated-workflow-id: post-patch".

### Stage 4 - Patch complete

Once we know we don't even have any workflows running on "Stage 2" or before (i.e. the workflow with the patch with
both code paths), we can just remove the patch deprecation altogether. To use the patch complete workflow, stop the
workflow from before and start it again with:

    dotnet run worker --workflow patch-complete

All workflows in "Stage 3" and any new workflows will work. Now let's start another workflow with this patch complete
code:

    dotnet run starter --start-workflow patch-complete-workflow-id

This will output "Started workflow with ID patch-complete-workflow-id and ...". Now query the patch deprecated workflow
that's still running:

    dotnet run starter --query-workflow patch-deprecated-workflow-id

This will output "Query result for ID patch-deprecated-workflow-id: post-patch". And if we execute a query against the
latest workflow:

    dotnet run starter --query-workflow patch-complete-workflow-id

As expected, this will output "Query result for ID patch-complete-workflow-id: post-patch".

Following these stages, we have successfully altered our workflow code.