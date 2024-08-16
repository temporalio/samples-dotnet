namespace TemporalioSamples.CounterInterceptor;

using System.Diagnostics;
using Temporalio.Activities;

public class MyActivities
{
    [Activity]
    public string SayHello(string name, string title) =>
        $"Hello {title} {name}";

    [Activity]
    public string SayGoodBye(string name, string title) =>
        $"Goodby {title} {name}";
}