namespace TemporalioSamples.Tests.WorkflowStreams;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;
using Basic = TemporalioSamples.WorkflowStreams.BasicPublishSubscribe;
using Bounded = TemporalioSamples.WorkflowStreams.BoundedLog;
using Concurrent = TemporalioSamples.WorkflowStreams.ConcurrentSubscriptions;
using External = TemporalioSamples.WorkflowStreams.ExternalPublisher;
using Llm = TemporalioSamples.WorkflowStreams.LlmTokenStreaming;
using Reconnecting = TemporalioSamples.WorkflowStreams.ReconnectingSubscriber;

public class WorkflowStreamsTests : WorkflowEnvironmentTestBase
{
    private static readonly string[] ExpectedOrderStatuses =
        ["received", "shipped", "complete"];

    public WorkflowStreamsTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task OrderWorkflow_PublishesWorkflowAndActivityEvents()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().
                AddActivity(Basic.PaymentActivities.ChargeCardAsync).
                AddWorkflow<Basic.OrderWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-order-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (Basic.OrderWorkflow wf) => wf.RunAsync(new Basic.OrderInput("order-42", null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var streamClient = new WorkflowStreamClient(Client, workflowId);

            var statuses = new List<string>();
            var progressCount = 0;
            await foreach (var item in streamClient.SubscribeAsync(new()
            {
                Topics = new List<string>
                {
                    Basic.Constants.TopicStatus,
                    Basic.Constants.TopicProgress,
                },
            }))
            {
                if (item.Topic == Basic.Constants.TopicStatus)
                {
                    var status = Decode<Basic.StatusEvent>(item);
                    statuses.Add(status.Kind);
                    if (status.Kind == "complete")
                    {
                        break;
                    }
                }
                else if (item.Topic == Basic.Constants.TopicProgress)
                {
                    progressCount++;
                }
            }

            Assert.Equal("charge-order-42", await handle.GetResultAsync());
            Assert.Equal(ExpectedOrderStatuses, statuses);
            Assert.True(progressCount >= 2);
        });
    }

    [Fact]
    public async Task SubscriptionAsyncEnumerable_DeliversItemsInOrder()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().
                AddActivity(Concurrent.PaymentActivities.ChargeCardAsync).
                AddWorkflow<Concurrent.OrderWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-concurrent-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (Concurrent.OrderWorkflow wf) =>
                    wf.RunAsync(new Concurrent.OrderInput("order-concurrent", null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var streamClient = new WorkflowStreamClient(Client, workflowId);
            var statuses = new List<string>();
            await foreach (var item in streamClient.SubscribeAsync(
                new WorkflowStreamSubscribeOptions
                {
                    Topics = new List<string>
                    {
                        Concurrent.Constants.TopicStatus,
                        Concurrent.Constants.TopicProgress,
                    },
                }))
            {
                await Task.Yield();
                if (item.Topic == Concurrent.Constants.TopicStatus)
                {
                    statuses.Add(Decode<Concurrent.StatusEvent>(item).Kind);
                }
            }

            Assert.Equal("charge-order-concurrent", await handle.GetResultAsync());
            Assert.Equal(ExpectedOrderStatuses, statuses);
        });
    }

    [Fact]
    public async Task ReconnectingSubscriber_ResumesAtNextOffset()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().AddWorkflow<Reconnecting.PipelineWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-pipeline-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (Reconnecting.PipelineWorkflow wf) => wf.RunAsync(
                    new Reconnecting.PipelineInput(
                        "pipeline-test",
                        TimeSpan.FromMilliseconds(50),
                        null)),
                new(workflowId, worker.Options.TaskQueue!));

            var offsets = new List<long>();
            long nextOffset = 0;
            await using (var firstClient = new WorkflowStreamClient(Client, workflowId))
            {
                await foreach (var item in firstClient.Topic(Reconnecting.Constants.TopicStatus).SubscribeAsync())
                {
                    offsets.Add(item.Offset);
                    nextOffset = item.Offset + 1;
                    if (offsets.Count == 2)
                    {
                        break;
                    }
                }
            }

            var remainingStages = new List<string>();
            await using (var secondClient = new WorkflowStreamClient(Client, workflowId))
            {
                await foreach (var item in secondClient.Topic(Reconnecting.Constants.TopicStatus).SubscribeAsync(nextOffset))
                {
                    offsets.Add(item.Offset);
                    var stage = Decode<Reconnecting.StageEvent>(item).Stage;
                    remainingStages.Add(stage);
                    if (stage == "complete")
                    {
                        break;
                    }
                }
            }

            Assert.Equal("pipeline pipeline-test done", await handle.GetResultAsync());
            Assert.Equal(offsets.Distinct().Count(), offsets.Count);
            Assert.Equal(nextOffset, offsets[2]);
            Assert.Equal("complete", remainingStages[^1]);
        });
    }

    [Fact]
    public async Task ExternalPublisher_PublishesAndClosesHub()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().AddWorkflow<External.HubWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-hub-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (External.HubWorkflow wf) =>
                    wf.RunAsync(new External.HubInput("test-hub", null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var subscriber = new WorkflowStreamClient(Client, workflowId);
            await using var publisher = new WorkflowStreamClient(Client, workflowId);
            publisher.Topic(External.Constants.TopicNews).
                Publish(new External.NewsEvent("test headline"), forceFlush: true);
            await publisher.FlushAsync();

            await foreach (var item in subscriber.Topic(External.Constants.TopicNews).SubscribeAsync())
            {
                Assert.Equal("test headline", Decode<External.NewsEvent>(item).Headline);
                break;
            }
            await handle.SignalAsync(wf => wf.CloseAsync());
            Assert.Equal("hub test-hub closed", await handle.GetResultAsync());
        });
    }

    [Fact]
    public async Task TruncatingTicker_FastForwardsStaleOffset()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().AddWorkflow<Bounded.TickerWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-ticker-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (Bounded.TickerWorkflow wf) => wf.RunAsync(
                    new Bounded.TickerInput(20, 5, 5, TimeSpan.Zero, null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var streamClient = new WorkflowStreamClient(Client, workflowId);

            await AssertMore.EventuallyAsync(async () =>
                Assert.True(await streamClient.GetOffsetAsync() >= 10));

            await foreach (var item in streamClient.Topic(Bounded.Constants.TopicTick).SubscribeAsync(1))
            {
                Assert.True(item.Offset >= 5);
                break;
            }
            Assert.Equal("ticker emitted 20 events", await handle.GetResultAsync());
        });
    }

    [Fact]
    public async Task LlmWorkflow_ReturnsMockedStreamingResult()
    {
        [Activity("StreamCompletion")]
        static Task<string> StreamCompletionAsync(Llm.LlmInput input) =>
            Task.FromResult("a streamed answer");

        using var worker = new TemporalWorker(
            Client,
            NewWorker().
                AddActivity(StreamCompletionAsync).
                AddWorkflow<Llm.LlmWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var result = await Client.ExecuteWorkflowAsync(
                (Llm.LlmWorkflow wf) =>
                    wf.RunAsync(new Llm.LlmInput("hello", "gpt-4o-mini", null)),
                new($"workflow-streams-llm-{Guid.NewGuid()}", worker.Options.TaskQueue!));
            Assert.Equal("a streamed answer", result);
        });
    }

    private TemporalWorkerOptions NewWorker() =>
        new($"workflow-streams-{Guid.NewGuid()}");

    private T Decode<T>(WorkflowStreamItem item) =>
        Client.Options.DataConverter.PayloadConverter.ToValue<T>(item.Payload);
}
