namespace TemporalioSamples.Timer;

using System.Diagnostics.CodeAnalysis;
using Temporalio.Activities;

[SuppressMessage("Design", "CA1052:Type 'MyActivities' is a static holder type but is neither static nor NotInheritable", Justification = "Class is designed for instantiation.")]
public class MyActivities
{
    [Activity]
    public static string Charge(string userId) => "charge successful";
}