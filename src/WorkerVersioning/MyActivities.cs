using Temporalio.Activities;

namespace TemporalioSamples.WorkerVersioning;

public class MyActivities
{
    public record IncompatibleActivityInput(string CalledBy, string MoreData);

    [Activity]
    public string SomeActivity(string calledBy)
    {
        return $"SomeActivity called by {calledBy}";
    }

    [Activity]
    public string SomeIncompatibleActivity(IncompatibleActivityInput inputData)
    {
        return $"SomeIncompatibleActivity called by: {inputData.CalledBy} with {inputData.MoreData}";
    }
}
