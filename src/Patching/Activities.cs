using Temporalio.Activities;

namespace TemporalioSamples.Patching;

public sealed class Activities
{
    [Activity]
    public string PrePatchActivity() => "pre-patch";

    [Activity]
    public string PostPatchActivity() => "post-patch";
}