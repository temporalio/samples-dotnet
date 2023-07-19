using Temporalio.Activities;

namespace TemporalioSamples.WorkerVersioning;

public static class MyActivities
{
    [Activity]
    public static string Greet(string text)
    {
        return $"Hello {text}";
    }

    [Activity]
    public static string SuperGreet(string text, int num)
    {
        return $"Hello {text} with {num}";
    }
}