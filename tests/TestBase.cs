namespace TemporalioSamples.Tests;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class TestBase : IDisposable
{
    private static readonly TextWriter OriginalOut = Console.Out;
    private readonly TextWriter? consoleWriter;

    protected TestBase(ITestOutputHelper output)
    {
        if (Program.InProc)
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                builder.
                    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                    SetMinimumLevel(Program.Verbose ? LogLevel.Trace : LogLevel.Information));
        }
        else
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                builder.AddXUnit(output));
            // Only set this if not in-proc
            consoleWriter = new ConsoleWriter(output, OriginalOut);
            Console.SetOut(consoleWriter);
        }
    }

    ~TestBase()
    {
        Dispose(false);
    }

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
}
