using Temporalio.Activities;

namespace TemporalioSamples.Patching;

public static class Activities
{
    [Activity]
    public static string PrePatchActivity() => "pre-patch";

    [Activity]
    public static string PostPatchActivity() => "post-patch";
}