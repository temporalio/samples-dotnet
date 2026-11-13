# Human in the loop

A tool that runs only after a person approves. `delete_file` is wrapped in `ApprovalRequiredAIFunction`, Agent Framework's own approval wrapper. The run returns with a `ToolApprovalRequestContent` in the response instead of calling the tool. The Workflow publishes the request through the `PendingApprovals` Query, waits for a `Decide` Update carrying `Decision(RequestId, Approved)` for each request, and sends `request.CreateResponse(approved)` back on the same session. The request id is `ficc_` plus the function call's id, which the plugin records in history, so the request survives a worker restart. While the Workflow waits, no Activity is open: the wait costs nothing. The Workflow-parked approval pattern follows the analysis in [Cecil Phillip's draft](https://github.com/temporal-community/temporal-dotnet-agents).

## What this sample demonstrates

- `new ApprovalRequiredAIFunction(deleteFile)` in `ChatOptions.Tools` and the `while (requests.Count > 0)` loop over `ToolApprovalRequestContent`
- A Query for what is pending and an Update keyed by request id. The validator checks only the input (a non-empty `RequestId`), so bad input never enters history. The handler fails the Update with an `ApplicationFailureException` for an id that is already decided or not pending, so a double submit or a typo cannot answer a later request.
- Why the handler waits for the model's reply to be read first: a decision can land in the same Workflow Task as the reply that raised the request, and handlers run before the loop in `RunAsync` has read that reply.
- The approved tool runs in the Workflow; a rejected one never runs, and the model is told the call was rejected

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
```

The starter polls the Query, prints `Approval requested: delete_file(path=/tmp/report.txt)`, sends `Decision` with the id the Query returned, prints `Decision: approved` and then the agent's report. Run `dotnet run workflow reject` to see the rejection path. If the agent answers without asking, or the run fails, the starter stops polling as soon as the Workflow closes and prints the result or the failure. The Workflow `microsoft-agent-framework-human-in-the-loop` shows two `MicrosoftAgentFramework.InvokeModel` Activities: the request and the report. Its id is fixed, so a run left waiting for a decision (for example after ctrl+c) blocks the next `dotnet run workflow` with `WorkflowAlreadyStartedException`; terminate it first with `temporal workflow terminate -w microsoft-agent-framework-human-in-the-loop`.

## Files

| File | Description |
|------|-------------|
| `HumanInTheLoopWorkflow.workflow.cs` | `ApprovalRequest`, `Decision` and `HumanInTheLoopWorkflow` |
| `Program.cs` | Runs the worker, or starts the Workflow, polls the Query, sends the Update and prints the result |
