namespace TemporalioSamples.Tests.SleepForDays;

using Temporalio.Activities;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.SleepForDays;
using Xunit;
using Xunit.Abstractions;

public class SleepForDaysWorkflowTests : TestBase
{
    public SleepForDaysWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_SleepForDays_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        var activities = new Activities();

        var activityExecutions = 0;
        // Mock out the activity to assert number of executions
        [Activity]
        void SendEmail(string msg)
        {
            activityExecutions++;
        }

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("sleep-for-days-task-queue").
                AddActivity(SendEmail).
                AddWorkflow<SleepForDaysWorkflow>());

        var startTime = await env.GetCurrentTimeAsync();
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (SleepForDaysWorkflow wf) => wf.RunAsync(),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // Continuously check history to see if timer has started.
            await AssertMore.EventuallyAsync(async () =>
            {
                var history = await handle.FetchHistoryAsync();
                Assert.Contains(history.Events, e =>
                    e.TimerStartedEventAttributes != null &&
                    (TimeSpan.FromDays(30) == e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan()));
            });

            // The sleep timer has started, we should expect an activity execution.
            Assert.Equal(1, activityExecutions);
            // Sleep for 90 days
            await env.DelayAsync(TimeSpan.FromDays(90));
            // Expect 3 activity executions
            Assert.Equal(4, activityExecutions);
            // Signal the workflow to complete
            await handle.SignalAsync(wf => wf.CompleteAsync());
            // Expect the same number of activity executions
            Assert.Equal(4, activityExecutions);
            // Assert at least 90 days of time have passed
            Assert.True((await env.GetCurrentTimeAsync() - startTime) >= TimeSpan.FromDays(90));
        });
    }
}