namespace TemporalioSamples.Dsl;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public static class DslActivities
{
    [Activity("activity1")]
    public static string Activity1(string arg)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Executing activity1 with arg: {Arg}", arg);
        return $"[result from activity1: {arg}]";
    }

    [Activity("activity2")]
    public static string Activity2(string arg)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Executing activity2 with arg: {Arg}", arg);
        return $"[result from activity2: {arg}]";
    }

    [Activity("activity3")]
    public static string Activity3(string arg1, string arg2)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Executing activity3 with args: {Arg1} and {Arg2}", arg1, arg2);
        return $"[result from activity3: {arg1} {arg2}]";
    }

    [Activity("activity4")]
    public static string Activity4(string arg)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Executing activity4 with arg: {Arg}", arg);
        return $"[result from activity4: {arg}]";
    }

    [Activity("activity5")]
    public static string Activity5(string arg1, string arg2)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Executing activity5 with args: {Arg1} and {Arg2}", arg1, arg2);
        return $"[result from activity5: {arg1} {arg2}]";
    }
}
