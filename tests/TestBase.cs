namespace TemporalioSamples.Tests;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class TestBase : IDisposable
{
    private readonly TextWriter? consoleWriter;

    protected TestBase(ITestOutputHelper output)
    {
        OutputHelper = output;
        // Only set console writer if not in-proc
        if (!Program.InProc)
        {
            consoleWriter = new ConsoleWriter(output);
            Console.SetOut(consoleWriter);
        }
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(ConfigureLogging);
    }

    ~TestBase() => Dispose(false);

    protected ITestOutputHelper OutputHelper { get; private init; }

    protected ILoggerFactory LoggerFactory { get; private init; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            consoleWriter?.Dispose();
        }
    }

    protected void ConfigureLogging(ILoggingBuilder builder)
    {
        if (Program.InProc)
        {
            builder.
                AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                SetMinimumLevel(Program.Verbose ? LogLevel.Trace : LogLevel.Information);
        }
        else
        {
            builder.AddXUnit(OutputHelper);
        }
    }
}
