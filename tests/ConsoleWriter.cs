namespace TemporalioSamples.Tests;

using Xunit.Abstractions;

public class ConsoleWriter : StringWriter
{
    private readonly ITestOutputHelper output;
#pragma warning disable CA2213 // We don't want to dispose original output
    private readonly TextWriter originalOut;
#pragma warning restore CA2213

    public ConsoleWriter(ITestOutputHelper output, TextWriter originalOut)
    {
        this.output = output;
        this.originalOut = originalOut;
    }

    public override void WriteLine(string? value)
    {
        try
        {
            output.WriteLine(value);
        }
        catch (Exception ex) when (ex is InvalidOperationException && ex.Message.Contains("no currently active test"))
        {
            // Fall back to original console output when no test is active or writer is disposed
            // This happens when background tasks write to console
            originalOut.WriteLine(value);
        }
    }
}
