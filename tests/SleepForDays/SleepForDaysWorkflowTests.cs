namespace TemporalioSamples.Tests.SleepForDays;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client;
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

    [Fact]
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
            bool timerStarted = false;
            while (!timerStarted)
            {
                var history = await handle.FetchHistoryAsync();
                foreach (var e in history.Events)
                {
                    if (e.TimerStartedEventAttributes != null && (TimeSpan.FromDays(30) == e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan()))
                    {
                        timerStarted = true;
                        break;
                    }
                }
            }

            // Sanity check - timer should have started.
            Assert.True(timerStarted);

            // Sleep for 90 days
            await env.DelayAsync(TimeSpan.FromDays(90));
            // Expect 3 activity executions
            Assert.Equal(3, activityExecutions);
            // Signal the workflow to complete
            await handle.SignalAsync(wf => wf.CompleteAsync());
            // Expect the same number of activity executions
            Assert.Equal(3, activityExecutions);
            // Assert at least 90 days of time have passed
            Assert.True((await env.GetCurrentTimeAsync() - startTime) >= TimeSpan.FromDays(90));
        });
    }
}