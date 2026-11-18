namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Microsoft.Extensions.AI;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.ContinueAsNew;
using Xunit;
using Xunit.Abstractions;

public class ContinueAsNewEndChatTests(ITestOutputHelper output, WorkflowEnvironment env) : WorkflowEnvironmentTestBase(output, env)
{
    [Fact]
    public async Task TurnAsync_AfterEndChat_RejectedWhileTheTurnInFlightFinishes()
    {
        // The model call blocks until the test releases it, so the first turn is in flight when the chat ends.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var fake = new FakeChatClient(call =>
        {
            started.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(30));
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "first reply"));
        });
        using var worker = new TemporalWorker(
            Client,
            TestSupport.WorkerOptions("chat-end", TestSupport.PluginFor(fake)).AddWorkflow<ChatWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (ChatWorkflow wf) => wf.RunAsync(new ChatInput(null)),
                TestSupport.WorkflowOptions(worker, "chat-end"));
            var firstTurn = await handle.StartUpdateAsync(
                wf => wf.TurnAsync("first"), new(WorkflowUpdateStage.Accepted));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

            await handle.SignalAsync(wf => wf.EndChatAsync());
            // Wait for a Workflow Task that includes the Signal to complete, so the handler has set
            // `done` before the next Update reaches the validator.
            await AssertMore.EventuallyAsync(async () =>
            {
                var events = (await handle.FetchHistoryAsync()).Events.ToList();
                var signaled = events.FindIndex(evt => evt.WorkflowExecutionSignaledEventAttributes is not null);
                Assert.True(signaled >= 0);
                Assert.Contains(events.Skip(signaled), evt => evt.WorkflowTaskCompletedEventAttributes is not null);
            });

            var rejected = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(wf => wf.TurnAsync("second")));
            Assert.Equal("The chat has ended", rejected.InnerException?.Message);

            // The turn in flight still gets its reply, and the Workflow closes once it has.
            release.Set();
            Assert.Equal("first reply", await firstTurn.GetResultAsync());
            await handle.GetResultAsync();
            Assert.Equal(1, fake.CallCount);
            Assert.Equal(2, (await handle.QueryAsync(wf => wf.Transcript())).Count);
        });
    }
}
