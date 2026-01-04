namespace TemporalioSamples.Dsl;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class DslWorkflow
{
    private Dictionary<string, object> variables = new();

    [WorkflowRun]
    public async Task<Dictionary<string, object>> RunAsync(DslInput input)
    {
        variables = new Dictionary<string, object>(input.Variables);
        Workflow.Logger.LogInformation("Running DSL workflow");
        await ExecuteStatementAsync(input.Root);
        Workflow.Logger.LogInformation("DSL workflow completed");
        return variables;
    }

    private async Task ExecuteStatementAsync(Statement statement)
    {
        switch (statement)
        {
            case ActivityStatement activityStmt:
                // Invoke activity loading arguments from variables and optionally storing result as a variable
                var args = activityStmt.Activity.Arguments
                    .Select(argName => variables.TryGetValue(argName, out var value) ? value : string.Empty)
                    .ToArray();

                var result = await Workflow.ExecuteActivityAsync<object>(
                    activityStmt.Activity.Name,
                    args,
                    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

                if (!string.IsNullOrEmpty(activityStmt.Activity.Result))
                {
                    variables[activityStmt.Activity.Result] = result;
                }
                break;
            case SequenceStatement sequenceStmt:
                foreach (var element in sequenceStmt.Sequence.Elements)
                {
                    await ExecuteStatementAsync(element);
                }
                break;
            case ParallelStatement parallelStmt:
                await Workflow.WhenAllAsync(parallelStmt.Parallel.Branches.Select(ExecuteStatementAsync));
                break;
            default:
                throw new InvalidOperationException($"Unknown statement type: {statement.GetType().Name}");
        }
    }
}
