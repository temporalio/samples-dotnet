using Temporal.Extensions.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var temporal = builder.AddTemporalLocalDevServer();

builder.AddProject<Projects.TemporalioSamples_SampleWorker>("sample-temporal-worker")
    .WaitFor(temporal)
    .WithReference(temporal);

builder.AddProject<Projects.TemporalioSamples_SampleClient>("sample-temporal-client")
    .WaitFor(temporal)
    .WithReference(temporal);

builder.Build().Run();