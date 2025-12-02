namespace TemporalioSamples.Tests.UpdatableTimer;

using Temporalio.Api.Enums.V1;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.UpdatableTimer;
using Xunit;
using Xunit.Abstractions;

public class UpdatableTimerWorkflowTests : TestBase
{
    public UpdatableTimerWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task SimpleRun_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(env.Client, new TemporalWorkerOptions("my-task-queue").AddWorkflow<MyWorkflow>());
        var wakeUpTime = DateTimeOffset.UtcNow.AddDays(30);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(wakeUpTime),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task WakeUpInThePast_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(env.Client, new TemporalWorkerOptions("my-task-queue").AddWorkflow<MyWorkflow>());
        var wakeUpTime = DateTimeOffset.UtcNow.AddSeconds(-10);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(wakeUpTime),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task WakeUpAfter30Days_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(env.Client, new TemporalWorkerOptions("my-task-queue").AddWorkflow<MyWorkflow>());
        var wakeUpTime = DateTimeOffset.UtcNow.AddDays(30);

        var startTime = await env.GetCurrentTimeAsync();
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(wakeUpTime),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            await handle.GetResultAsync();

            var history = await handle.FetchHistoryAsync();

            // Continuously check history to see if timer has started.
            Assert.Contains(history.Events, e =>
                e.TimerStartedEventAttributes != null &&
                e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan() >= TimeSpan.FromDays(29));

            // Assert at least 30 days of time have passed
            Assert.True((await env.GetCurrentTimeAsync() - startTime) >= TimeSpan.FromDays(30));
        });
    }

    [Fact]
    public async Task WakeUpInADayThenUpdateToAnHour_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(env.Client, new TemporalWorkerOptions("my-task-queue").AddWorkflow<MyWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var inADay = DateTimeOffset.UtcNow.AddDays(1);
            var inAnHour = DateTimeOffset.UtcNow.AddHours(1);

            var handle = await env.Client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(inADay),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            var wakeUpTime1 = await handle.QueryAsync(workflow => workflow.GetWakeUpTime);
            Assert.Equal(inADay, wakeUpTime1, precision: TimeSpan.FromSeconds(5));

            await handle.SignalAsync(workflow => workflow.UpdateWakeUpAsync(inAnHour));

            var wakeUpTime2 = await handle.QueryAsync(workflow => workflow.GetWakeUpTime);
            Assert.Equal(inAnHour, wakeUpTime2, precision: TimeSpan.FromSeconds(5));

            await handle.GetResultAsync();

            var history = await handle.FetchHistoryAsync();

            var firstTimer = history.Events.FirstOrDefault(x => x.EventType == EventType.TimerStarted);
            var secondTimer = history.Events.LastOrDefault(x => x.EventType == EventType.TimerStarted);

            Assert.NotNull(firstTimer);
            Assert.NotNull(secondTimer);

            // Check that the first timer canceled.
            Assert.Contains(history.Events, e =>
                e.EventType == EventType.TimerCanceled &&
                e.TimerCanceledEventAttributes.TimerId == firstTimer.TimerStartedEventAttributes.TimerId);

            // Check that the second timer fired.
            Assert.Contains(history.Events, e =>
                e.EventType == EventType.TimerFired &&
                e.TimerFiredEventAttributes.TimerId == secondTimer.TimerStartedEventAttributes.TimerId);
        });
    }
}