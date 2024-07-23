namespace TermporalioSamples.CounterInterceptor;

using System.Diagnostics;
using Temporalio.Activities;

public class MyActivities
{
    [Activity]
    public string SayHello(string name, string title)
    {
        return "Hello " + title + " " + name;
    }

    [Activity]
    public string SayGoodBye(string name, string title)
    {
        return "Goodbye " + title + " " + name;
    }
}
