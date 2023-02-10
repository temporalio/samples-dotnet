namespace Temporalio.Samples.Tests.ActivityWorker;

using Temporalio.Samples.ActivityWorker;
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
        var env = new Testing.ActivityEnvironment();
        Assert.Equal("Hello, Test!", await env.RunAsync(() => Activities.SayHello("Test")));
    }
}