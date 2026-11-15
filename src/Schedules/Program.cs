using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Schedules;

async Task<TemporalClient> ConnectClientAsync()
{
    // Create a client to localhost on default namespace
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "schedules").
            AddActivity(MyActivities.NotifyUserAsync).
            AddActivity(MyActivities.AddReminderToDatabase).
            AddWorkflow<MyWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ScheduleStartAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    Console.WriteLine("Scheduling workflow");

    var text = "Dear future self, please take out the recycling tonight. Sincerely, past you";

    var action = ScheduleActionStartWorkflow.Create<MyWorkflow>(
        wf => wf.RunAsync(text),
        new()
        {
            Id = "schedule-workflow-id",
            TaskQueue = "schedules",
        });

    var spec = new ScheduleSpec
    {
        Intervals = new List<ScheduleIntervalSpec>
        {
            new(Every: TimeSpan.FromSeconds(10)),
        },
    };

    var schedule = new Schedule(action, spec)
    {
        Policy = new()
        {
            CatchupWindow = TimeSpan.FromDays(1),
            Overlap = ScheduleOverlapPolicy.AllowAll,
        },
    };

    var scheduleHandle = await client.CreateScheduleAsync("sample-schedule", schedule);

    Console.WriteLine(@$"Started schedule {scheduleHandle.Id}

The reminder Workflow will run and log from the Worker every 10 seconds.

dotnet run schedule-go-faster
dotnet run schedule-pause
dotnet run schedule-unpause
dotnet run schedule-delete
");
}

async Task ScheduleGoFasterAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var handle = client.GetScheduleHandle("sample-schedule");

    await handle.UpdateAsync(input =>
    {
        var spec = new ScheduleSpec
        {
            Intervals = new List<ScheduleIntervalSpec>
            {
                new(Every: TimeSpan.FromSeconds(5)),
            },
        };
        var schedule = input.Description.Schedule with { Spec = spec };
        return new ScheduleUpdate(schedule);
    });

    Console.WriteLine("Schedule is now triggered every 5 seconds.");
}

async Task SchedulePauseAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var handle = client.GetScheduleHandle("sample-schedule");
    await handle.PauseAsync();
    Console.WriteLine("Schedule is now paused.");
}

async Task ScheduleUnpauseAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var handle = client.GetScheduleHandle("sample-schedule");
    await handle.UnpauseAsync();
    Console.WriteLine("Schedule is now unpaused.");
}

async Task ScheduleDeleteAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var handle = client.GetScheduleHandle("sample-schedule");
    await handle.DeleteAsync();
    Console.WriteLine("Schedule is now deleted.");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var scheduleStartCommand = new Command("schedule-start", "Create schedule");
scheduleStartCommand.SetAction(ScheduleStartAsync);

var scheduleGoFasterCommand = new Command("schedule-go-faster", "Update schedule to run every 5 seconds");
scheduleGoFasterCommand.SetAction(ScheduleGoFasterAsync);

var schedulePauseCommand = new Command("schedule-pause", "Pause schedule");
schedulePauseCommand.SetAction(SchedulePauseAsync);

var scheduleUnpauseCommand = new Command("schedule-unpause", "Unpause schedule");
scheduleUnpauseCommand.SetAction(ScheduleUnpauseAsync);

var scheduleDeleteCommand = new Command("schedule-delete", "Delete schedule");
scheduleDeleteCommand.SetAction(ScheduleDeleteAsync);

var rootCommand = new RootCommand("Schedules sample")
{
    workerCommand,
    scheduleStartCommand,
    scheduleGoFasterCommand,
    schedulePauseCommand,
    scheduleUnpauseCommand,
    scheduleDeleteCommand,
};
return await rootCommand.Parse(args).InvokeAsync();
