namespace TemporalioSamples.Tests;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class TestBase : IDisposable
{
    private readonly TextWriter? consoleWriter;

    protected TestBase(ITestOutputHelper output)
    {
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddXUnit(output));
        consoleWriter = new ConsoleWriter(output);
        Console.SetOut(consoleWriter);
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
