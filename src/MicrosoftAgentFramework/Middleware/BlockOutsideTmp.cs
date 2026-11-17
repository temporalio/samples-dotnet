// @@@SNIPSTART dotnet-microsoft-agent-framework-middleware-tool
namespace TemporalioSamples.MicrosoftAgentFramework.Middleware;

using Microsoft.Extensions.AI;

// Refuses delete_file calls outside /tmp/ without running the tool. Wrapping the AIFunction is the
// per-tool hook that runs in the Workflow; AIAgentBuilder middleware wraps AIAgent.RunAsync, which
// the plugin does not support inside a Workflow. The wrapper runs next to the agent's loop, so it
// must be deterministic: no clock, no I/O, no randomness.
public sealed class BlockOutsideTmp : DelegatingAIFunction
{
    public const string AllowedPrefix = "/tmp/";

    public BlockOutsideTmp(AIFunction innerFunction)
        : base(innerFunction)
    {
    }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var path = arguments.TryGetValue("path", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        if (!Normalize(path).StartsWith(AllowedPrefix, StringComparison.Ordinal))
        {
            // Not calling the inner function skips the tool; the model reads this result instead.
            return new ValueTask<object?>($"Refused: {path} is outside {AllowedPrefix}");
        }

        return base.InvokeCoreAsync(arguments, cancellationToken);
    }

    // Resolves "." and ".." segments on "/" in memory, so "/tmp/../etc/passwd" is "/etc/passwd".
    // Path.GetFullPath would read the host's rules and working directory, which differ between
    // workers and so between replays.
    private static string Normalize(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (segment.Length > 0 && segment != ".")
            {
                segments.Add(segment);
            }
        }

        return (path.StartsWith('/') ? "/" : string.Empty) + string.Join('/', segments);
    }
}
// @@@SNIPEND
