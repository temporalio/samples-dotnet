namespace TemporalioSamples.Tests;

using System.Runtime.InteropServices;
using Xunit;

/// <summary>
/// The time-skipping test server can only run on x86/x64/Arm64 currently.
/// </remarks>
public sealed class TimeSkippingServerFactAttribute : FactAttribute
{
    public TimeSkippingServerFactAttribute()
    {
        var processArchitecture = RuntimeInformation.ProcessArchitecture;

        if (
            processArchitecture != Architecture.X86 &&
            processArchitecture != Architecture.X64 &&
            processArchitecture != Architecture.Arm64)
        {
            Skip = "Time-skipping test server only works on x86/x64/Arm64 platforms. Current platform " + processArchitecture;
        }
    }
}