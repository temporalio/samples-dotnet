using Temporalio.Client;
using TemporalioSamples.AspNet.Worker;

var builder = WebApplication.CreateBuilder(args);

// Setup console logging
builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Information);

// Set a singleton for the client _task_. Errors will not happen here, only when
// the await is performed.
builder.Services.AddSingleton(ctx =>
    // TODO(cretz): It is not great practice to pass around tasks to be awaited
    // on separately (VSTHRD003). We may prefer a direct DI extension, see
    // https://github.com/temporalio/sdk-dotnet/issues/46.
    TemporalClient.ConnectAsync(new()
    {
        TargetHost = "localhost:7233",
        LoggerFactory = ctx.GetRequiredService<ILoggerFactory>(),
    }));

var app = builder.Build();

app.MapGet("/", async (Task<TemporalClient> clientTask, string? name) =>
{
    var client = await clientTask;
    return await client.ExecuteWorkflowAsync(
        MyWorkflow.Ref.RunAsync,
        name ?? "Temporal",
        new(id: $"aspnet-sample-workflow-{Guid.NewGuid()}", taskQueue: MyWorkflow.TaskQueue));
});

app.Run();