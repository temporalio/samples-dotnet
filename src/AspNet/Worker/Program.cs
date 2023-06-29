using Temporalio.Extensions.Hosting;
using TemporalioSamples.AspNet.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(ctx =>
        ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
    .ConfigureServices(ctx =>
        ctx.AddHostedTemporalWorker(
            clientTargetHost: "localhost:7233",
            clientNamespace: "default",
            taskQueue: MyWorkflow.TaskQueue).
        AddWorkflow<MyWorkflow>())
    .Build();

host.Run();