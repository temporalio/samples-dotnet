using Temporalio.Workflows;

namespace RoutedVersioning;

[Workflow(WorkflowType)]
public interface IMyWorkflow
{
    const string WorkflowType = "MyWorkflow";

    [WorkflowRun]
    Task RunAsync(StartMyWorkflowRequest args);

    [WorkflowSignal]
    Task CallMeMaybeAsync();

    [WorkflowQuery]
#pragma warning disable CA1024
    string GetResult();
#pragma warning restore CA1024
}