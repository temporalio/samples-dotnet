namespace TemporalioSamples.Tests.ActivityWorker;

using Temporalio.Testing;
using TemporalioSamples.ActivityWorker;
using Xunit;
using Xunit.Abstractions;

public class ActivityWorkerTests : TestBase
{
    public ActivityWorkerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task Main_RunActivity_Succeeds()
    {
        var env = new ActivityEnvironment();
        Assert.Equal("Hello, Test!", await env.RunAsync(() => SayHelloActivities.SayHello("Test")));
    }
}