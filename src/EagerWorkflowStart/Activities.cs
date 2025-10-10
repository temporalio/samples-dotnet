namespace TemporalioSamples.EagerWorkflowStart;

using Temporalio.Activities;

public class Activities
{
    [Activity]
    public string Greeting(string name) => $"Hello, {name}!";
}
