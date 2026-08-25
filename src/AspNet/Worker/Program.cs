using Temporalio.Extensions.Hosting;
using Temporalio.Runtime;
using TemporalioSamples.AspNet.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(ctx =>
        ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
    .ConfigureServices(ctx =>
    {
        // Add the client the worker will use. The Core SDK writes its logs to the console itself
        // unless Forwarding is set, in which case they go to the injected ILogger.
        ctx.AddTemporalClient(
            clientTargetHost: "localhost:7233",
            clientNamespace: "default").
            Configure<ILoggerFactory>((options, loggerFactory) =>
                options.Runtime = new TemporalRuntime(new TemporalRuntimeOptions()
                {
                    Telemetry = new TelemetryOptions()
                    {
                        Logging = new LoggingOptions()
                        {
                            // Core SDK logs default to WARN; lowered here so there is more to see.
                            Filter = new TelemetryFilterOptions(core: TelemetryFilterOptions.Level.Info),
                            Forwarding = new LogForwardingOptions(loggerFactory.CreateLogger("Temporalio.Core")),
                        },
                    },
                }));

        // Add the worker, which uses the injected client
        ctx.AddHostedTemporalWorker(MyWorkflow.TaskQueue).
            AddWorkflow<MyWorkflow>();
    })
    .Build();

host.Run();