namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Microsoft.Extensions.AI;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.ContinueAsNew;
using Xunit;
using Xunit.Abstractions;

// The chat sample's Update validator, on the shared dev server. ContinueAsNewWorkflowTests runs with a
// low Continue-As-New threshold, where an extra Workflow Task (such as one for a rejected Update) can move
// the chat to a new run before the next turn.
public class ContinueAsNewTurnValidatorTests(ITestOutputHelper output, WorkflowEnvironment env) : WorkflowEnvironmentTestBase(output, env)
{
    [Fact]
    public async Task TurnAsync_BlankPrompt_RejectedOutsideHistory()
    {
        using var fake = new FakeChatClient(ScriptedResponse.Text("unused"));
        using var worker = new TemporalWorker(
            Client,
            TestSupport.WorkerOptions("chat-blank", TestSupport.PluginFor(fake)).AddWorkflow<ChatWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (ChatWorkflow wf) => wf.RunAsync(new ChatInput(null)),
                TestSupport.WorkflowOptions(worker, "chat-blank"));
            var rejected = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(wf => wf.TurnAsync("   ")));
            Assert.Equal("The prompt is empty", rejected.InnerException?.Message);

            await handle.SignalAsync(wf => wf.EndChatAsync());
            await handle.GetResultAsync();
            // The validator rejected the turn, so the model was not called and Event History has no Update.
            Assert.Equal(0, fake.CallCount);
            Assert.DoesNotContain(
                (await handle.FetchHistoryAsync()).Events,
                evt => evt.WorkflowExecutionUpdateAcceptedEventAttributes is not null);
        });
    }

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
