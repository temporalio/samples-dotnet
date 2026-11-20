namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Microsoft.Extensions.AI;
using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Streaming;
using Xunit;
using Xunit.Abstractions;

public class StreamingWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env) : WorkflowEnvironmentTestBase(output, env)
{
    [Fact]
    public async Task RunAsync_PublishesUpdatesToTheTopic_OneModelActivity()
    {
        const string text = "One. Two. Three. Four. Five.";
        // The model call holds the rest of its stream until the subscriber has the first update.
        var firstUpdateSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fake = new FakeChatClient(ScriptedResponse.Text(text));
        using var chatClient = new FirstUpdateGateChatClient(fake, firstUpdateSeen.Task);
        using var worker = new TemporalWorker(
            Client,
            TestSupport.WorkerOptions("streaming", TestSupport.PluginFor(chatClient)).AddWorkflow<StreamingWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (StreamingWorkflow wf) => wf.RunAsync(new StreamingInput("Count from 1 to 5.", null)),
                TestSupport.WorkflowOptions(worker, "streaming"));

            var received = new List<ChatResponseUpdate>();
            await using var streams = new WorkflowStreamClient(Client, handle.Id);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var subscription = streams.SubscribeAsync<ChatResponseUpdate>(
                new WorkflowStreamSubscribeOptions { Topics = new List<string> { StreamingWorkflow.Topic } });
            // Read to the end: the subscription ends on its own when the Workflow closes, which is
            // what the starter relies on.
            await foreach (var item in subscription.WithCancellation(timeout.Token))
            {
                received.Add(item.Value);
                if (received.Count == 1)
                {
                    // The model call is still open, so the Activity published this update as it went
                    // instead of when it completed.
                    var history = await handle.FetchHistoryAsync();
                    Assert.DoesNotContain(history.Events, evt => evt.ActivityTaskCompletedEventAttributes is not null);
                    firstUpdateSeen.SetResult();
                }
            }

            Assert.Equal(text, await handle.GetResultAsync());
            Assert.True(received.Count > 1, $"expected several streamed updates, got {received.Count}");
            Assert.Equal(text, string.Concat(received.Select(update => update.Text)));
            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(1, counts[TestSupport.InvokeModel]);
            Assert.Single(counts);
        });
    }
}
