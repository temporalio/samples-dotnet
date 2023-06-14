using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Worker;
using TemporalioSamples.Schedules;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
});

async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

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
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ScheduleStartAsync()
{
    Console.WriteLine("Scheduling workflow");

    var text = "Dear future self, please take out the recycling tonight. Sincerely, past you";

    var action = ScheduleActionStartWorkflow.Create<MyWorkflow>(
        wf => wf.RunAsync(text),
        new()
        {
            ID = "schedule-workflow-id",
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

    Console.WriteLine(@$"Started schedule {scheduleHandle.ID}

The reminder Workflow will run and log from the Worker every 10 seconds.

dotnet run schedule-go-faster
dotnet run schedule-pause
dotnet run schedule-unpause
dotnet run schedule-delete
");
}

async Task ScheduleGoFasterAsync()
{
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

async Task SchedulePauseAsync()
{
    var handle = client.GetScheduleHandle("sample-schedule");
    await handle.PauseAsync();
    Console.WriteLine("Schedule is now paused.");
}

async Task ScheduleUnpauseAsync()
{
    var handle = client.GetScheduleHandle("sample-schedule");
    await handle.UnpauseAsync();
    Console.WriteLine("Schedule is now unpaused.");
}

async Task ScheduleDeleteAsync()
{
    var handle = client.GetScheduleHandle("sample-schedule");
    await handle.DeleteAsync();
    Console.WriteLine("Schedule is now deleted.");
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "schedule-start":
        await ScheduleStartAsync();
        break;
    case "schedule-go-faster":
        await ScheduleGoFasterAsync();
        break;
    case "schedule-pause":
        await SchedulePauseAsync();
        break;
    case "schedule-unpause":
        await ScheduleUnpauseAsync();
        break;
    case "schedule-delete":
        await ScheduleDeleteAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker', 'schedule-start', 'schedule-go-faster', 'schedule-pause', 'schedule-unpause' or 'schedule-delete' as the single argument");
}