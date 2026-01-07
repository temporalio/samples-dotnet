namespace TemporalioSamples.Dsl;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class DslWorkflow
{
    private readonly Dictionary<string, object> variables;

    [WorkflowInit]
    public DslWorkflow(DslInput input) => variables = input.Variables;

    [WorkflowRun]
    public async Task<Dictionary<string, object>> RunAsync(DslInput input)
    {
        Workflow.Logger.LogInformation("Running DSL workflow");
        await ExecuteStatementAsync(input.Root);
        Workflow.Logger.LogInformation("DSL workflow completed");
        return variables;
    }

    private async Task ExecuteStatementAsync(DslInput.Statement statement)
    {
        switch (statement)
        {
            case DslInput.ActivityStatement stmt:
                // Invoke activity loading arguments from variables and optionally storing result as a variable
                var result = await Workflow.ExecuteActivityAsync<object>(
                    stmt.Activity.Name,
                    stmt.Activity.Arguments.Select(arg => variables[arg]).ToArray(),
                    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

                if (!string.IsNullOrEmpty(stmt.Activity.Result))
                {
                    variables[stmt.Activity.Result] = result;
                }
                break;
            case DslInput.SequenceStatement stmt:
                foreach (var element in stmt.Sequence.Elements)
                {
                    await ExecuteStatementAsync(element);
                }
                break;
            case DslInput.ParallelStatement stmt:
                await Workflow.WhenAllAsync(stmt.Parallel.Branches.Select(ExecuteStatementAsync));
                break;
            default:
                throw new InvalidOperationException($"Unknown statement type: {statement.GetType().Name}");
        }
    }
}
