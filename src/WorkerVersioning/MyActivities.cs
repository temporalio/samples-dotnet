using Temporalio.Activities;

namespace TemporalioSamples.WorkerVersioning;

public record IncompatibleActivityInput(string CalledBy, string MoreData);

public class MyActivities
{
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
