using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.SampleWorkflow;

var builder = Host.CreateApplicationBuilder(args);

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();

builder.Services.AddHostedTemporalWorker(
    clientTargetHost: connectOptions.TargetHost ?? "localhost:7233",
    clientNamespace: connectOptions.Namespace,
    taskQueue: "simple-task-queue")
    .AddScopedActivities<SimpleActivities>()
    .AddWorkflow<SimpleWorkflow>();

var host = builder.Build();
await host.RunAsync();