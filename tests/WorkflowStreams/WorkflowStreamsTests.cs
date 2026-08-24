namespace TemporalioSamples.Tests.WorkflowStreams;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using TemporalioSamples.WorkflowStreams;
using Xunit;
using Xunit.Abstractions;

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
                AddActivity(PaymentActivities.ChargeCardAsync).
                AddWorkflow<OrderWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-order-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (OrderWorkflow wf) => wf.RunAsync(new OrderInput("order-42", null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var streamClient = new WorkflowStreamClient(Client, workflowId);

            var statuses = new List<string>();
            var progressCount = 0;
            await foreach (var item in streamClient.Subscribe(new()
            {
                Topics = new List<string>
                {
                    WorkflowStreamsConstants.TopicStatus,
                    WorkflowStreamsConstants.TopicProgress,
                },
            }))
            {
                if (item.Topic == WorkflowStreamsConstants.TopicStatus)
                {
                    var status = Decode<StatusEvent>(item);
                    statuses.Add(status.Kind);
                    if (status.Kind == "complete")
                    {
                        break;
                    }
                }
                else if (item.Topic == WorkflowStreamsConstants.TopicProgress)
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
    public async Task ListenerSubscription_DeliversSerializedCallbacks()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().
                AddActivity(PaymentActivities.ChargeCardAsync).
                AddWorkflow<OrderWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-listener-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (OrderWorkflow wf) => wf.RunAsync(new OrderInput("order-listener", null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var streamClient = new WorkflowStreamClient(Client, workflowId);
            var listener = new RecordingListener();
            using var subscription = streamClient.Subscribe(
                new SubscribeOptions
                {
                    Topics = new List<string>
                    {
                        WorkflowStreamsConstants.TopicStatus,
                        WorkflowStreamsConstants.TopicProgress,
                    },
                },
                listener);

            await subscription.Completion;
            Assert.Equal("charge-order-listener", await handle.GetResultAsync());
            Assert.Equal(1, listener.MaxConcurrentCallbacks);
            Assert.Equal(ExpectedOrderStatuses, listener.Statuses);
            Assert.True(listener.Completed);
        });
    }

    [Fact]
    public async Task ReconnectingSubscriber_ResumesAtNextOffset()
    {
        using var worker = new TemporalWorker(
            Client,
            NewWorker().AddWorkflow<PipelineWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-pipeline-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (PipelineWorkflow wf) => wf.RunAsync(
                    new PipelineInput(
                        "pipeline-test",
                        TimeSpan.FromMilliseconds(50),
                        null)),
                new(workflowId, worker.Options.TaskQueue!));

            var offsets = new List<long>();
            long nextOffset = 0;
            await using (var firstClient = new WorkflowStreamClient(Client, workflowId))
            {
                await foreach (var item in firstClient.Topic(WorkflowStreamsConstants.TopicStatus).Subscribe())
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
                await foreach (var item in secondClient.Topic(WorkflowStreamsConstants.TopicStatus).Subscribe(nextOffset))
                {
                    offsets.Add(item.Offset);
                    var stage = Decode<StageEvent>(item).Stage;
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
            NewWorker().AddWorkflow<HubWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-hub-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (HubWorkflow wf) => wf.RunAsync(new HubInput("test-hub", null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var subscriber = new WorkflowStreamClient(Client, workflowId);
            await using var publisher = new WorkflowStreamClient(Client, workflowId);
            publisher.Topic(WorkflowStreamsConstants.TopicNews).Publish(new NewsEvent("test headline"), forceFlush: true);
            await publisher.FlushAsync();

            await foreach (var item in subscriber.Topic(WorkflowStreamsConstants.TopicNews).Subscribe())
            {
                Assert.Equal("test headline", Decode<NewsEvent>(item).Headline);
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
            NewWorker().AddWorkflow<TickerWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-streams-ticker-{Guid.NewGuid()}";
            var handle = await Client.StartWorkflowAsync(
                (TickerWorkflow wf) => wf.RunAsync(
                    new TickerInput(20, 5, 5, TimeSpan.Zero, null)),
                new(workflowId, worker.Options.TaskQueue!));
            await using var streamClient = new WorkflowStreamClient(Client, workflowId);

            await AssertMore.EventuallyAsync(async () =>
                Assert.True(await streamClient.GetOffsetAsync() >= 10));

            await foreach (var item in streamClient.Topic(WorkflowStreamsConstants.TopicTick).Subscribe(1))
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
        static Task<string> StreamCompletionAsync(LlmInput input) =>
            Task.FromResult("a streamed answer");

        using var worker = new TemporalWorker(
            Client,
            NewWorker().
                AddActivity(StreamCompletionAsync).
                AddWorkflow<LlmWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var result = await Client.ExecuteWorkflowAsync(
                (LlmWorkflow wf) => wf.RunAsync(new LlmInput("hello", "gpt-4o-mini", null)),
                new($"workflow-streams-llm-{Guid.NewGuid()}", worker.Options.TaskQueue!));
            Assert.Equal("a streamed answer", result);
        });
    }

    private TemporalWorkerOptions NewWorker() =>
        new($"workflow-streams-{Guid.NewGuid()}");

    private T Decode<T>(WorkflowStreamItem item) =>
        Client.Options.DataConverter.PayloadConverter.ToValue<T>(item.Payload);

    private sealed class RecordingListener : WorkflowStreamListener
    {
        private readonly object lockObj = new();
        private readonly List<string> statuses = new();
        private int activeCallbacks;

        public bool Completed { get; private set; }

        public int MaxConcurrentCallbacks { get; private set; }

        public IReadOnlyCollection<string> Statuses
        {
            get
            {
                lock (lockObj)
                {
                    return statuses.ToArray();
                }
            }
        }

        public override async Task OnNextAsync(WorkflowStreamItem item)
        {
            var active = Interlocked.Increment(ref activeCallbacks);
            MaxConcurrentCallbacks = Math.Max(MaxConcurrentCallbacks, active);
            try
            {
                await Task.Yield();
                if (item.Topic == WorkflowStreamsConstants.TopicStatus)
                {
                    lock (lockObj)
                    {
                        statuses.Add(DataConverter.Default.PayloadConverter.
                            ToValue<StatusEvent>(item.Payload).Kind);
                    }
                }
            }
            finally
            {
                _ = Interlocked.Decrement(ref activeCallbacks);
            }
        }

        public override void OnCompleted() => Completed = true;
    }
}
