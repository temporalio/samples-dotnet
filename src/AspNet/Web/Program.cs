using Temporalio.Client;
using TemporalioSamples.AspNet.Worker;

var builder = WebApplication.CreateBuilder(args);

// Setup console logging
builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Information);

var connection = await TemporalConnection.ConnectAsync(new TemporalConnectionOptions("localhost:7233"));
builder.Services.AddSingleton<ITemporalClient>(provider => new TemporalClient(connection, new()
{
    LoggerFactory = provider.GetRequiredService<ILoggerFactory>(),
}));

var app = builder.Build();

app.MapGet("/", async (ITemporalClient client, string? name) =>
{
    return await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(name ?? "Temporal"),
        new(id: $"aspnet-sample-workflow-{Guid.NewGuid()}", taskQueue: MyWorkflow.TaskQueue));
});

await app.RunAsync();