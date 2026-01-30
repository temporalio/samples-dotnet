// ReSharper disable ArrangeModifiersOrder

namespace RoutedVersioning;

public class StartMyWorkflowRequest
{
    required public string Value { get; init; }

    required public ExecutionOptions Options { get; init; }

    public class ExecutionOptions
    {
        required public WorkflowVersion Version { get; set; }
    }
}