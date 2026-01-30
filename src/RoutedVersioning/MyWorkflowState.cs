namespace RoutedVersioning;

public class MyWorkflowState
{
    required public StartMyWorkflowRequest Args { get; set; }

    public string? Result { get; set; }
}