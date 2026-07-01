using System.Runtime.InteropServices;

namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Utility for locating and validating the Temporal CLI executable on the system PATH.
/// </summary>
internal static class TemporalCliLocator
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when the <c>temporal</c> CLI executable
    /// cannot be found on the system PATH.
    /// </summary>
    /// <param name="isAvailable">
    /// Optional override for the availability check. When <c>null</c> (default) the real PATH is
    /// inspected. Pass a custom delegate in tests to simulate presence or absence of the CLI
    /// without depending on the test machine's PATH.
    /// </param>
    internal static void EnsureAvailable(Func<bool>? isAvailable = null)
    {
        if (!(isAvailable ?? IsOnPath)())
        {
            throw new InvalidOperationException(
                "The 'temporal' CLI executable was not found on PATH. " +
                "Install it from https://docs.temporal.io/cli and ensure " +
                "'temporal' is accessible on your PATH before using AddTemporalCliServer.");
        }
    }

    private static bool IsOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "temporal.exe"
            : "temporal";

        return pathEnv
            .Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, executableName))
            .Any(File.Exists);
    }
}
