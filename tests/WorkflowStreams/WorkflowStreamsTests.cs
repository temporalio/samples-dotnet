namespace TemporalioSamples.Tests.WorkflowStreams;

using Temporalio.Api.Enums.V1;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;
using Basic = TemporalioSamples.WorkflowStreams.BasicPublishSubscribe;
using Bounded = TemporalioSamples.WorkflowStreams.BoundedLog;
using Concurrent = TemporalioSamples.WorkflowStreams.ConcurrentSubscriptions;
using External = TemporalioSamples.WorkflowStreams.ExternalPublisher;
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
            var payloadConverter = Client.Options.DataConverter.WithSerializationContext(
                new ISerializationContext.Workflow(Client.Options.Namespace, workflowId)).PayloadConverter;

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
                    var status = payloadConverter.ToValue<Basic.StatusEvent>(item.Payload);
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
            var payloadConverter = Client.Options.DataConverter.WithSerializationContext(
                new ISerializationContext.Workflow(Client.Options.Namespace, workflowId)).PayloadConverter;
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
                if (item.Topic == Concurrent.Constants.TopicStatus)
                {
                    statuses.Add(payloadConverter.ToValue<Concurrent.StatusEvent>(item.Payload).Kind);
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
                await foreach (var item in firstClient.
                    GetTopic<Reconnecting.StageEvent>(Reconnecting.Constants.TopicStatus).
                    SubscribeAsync())
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
                await foreach (var item in secondClient.
                    GetTopic<Reconnecting.StageEvent>(Reconnecting.Constants.TopicStatus).
                    SubscribeAsync(nextOffset))
                {
                    offsets.Add(item.Offset);
                    var stage = item.Value.Stage;
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
    public async Task ExternalPublisher_FollowsContinueAsNewAndClosesHub()
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
            var initialRunId = handle.FirstExecutionRunId;
            await using var subscriber = new WorkflowStreamClient(Client, workflowId);
            await using var publisher = new WorkflowStreamClient(Client, workflowId);
            var topic = publisher.GetTopic<External.NewsEvent>(External.Constants.TopicNews);
            await using var subscription = subscriber.
                GetTopic<External.NewsEvent>(External.Constants.TopicNews).
                SubscribeAsync().GetAsyncEnumerator();

            topic.Publish(new External.NewsEvent("before continue-as-new"), forceFlush: true);
            await publisher.FlushAsync();
            Assert.True(await subscription.MoveNextAsync());
            Assert.Equal("before continue-as-new", subscription.Current.Value.Headline);

            var waitingMove = subscription.MoveNextAsync().AsTask();
            await handle.SignalAsync(wf => wf.ContinueAsNewAsync());
            await AssertMore.EventuallyAsync(async () =>
                Assert.NotEqual(initialRunId, (await handle.DescribeAsync()).RunId));

            topic.Publish(new External.NewsEvent("after continue-as-new"), forceFlush: true);
            await publisher.FlushAsync();
            Assert.True(await waitingMove);
            Assert.Equal("after continue-as-new", subscription.Current.Value.Headline);
            Assert.Equal(1, subscription.Current.Offset);

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
                Assert.Equal(20, await streamClient.GetOffsetAsync()));
            Assert.Equal(WorkflowExecutionStatus.Running, (await handle.DescribeAsync()).Status);

            var offsets = new List<long>();
            var ticks = new List<int>();
            await foreach (var item in streamClient.
                GetTopic<Bounded.TickEvent>(Bounded.Constants.TopicTick).
                SubscribeAsync(1))
            {
                offsets.Add(item.Offset);
                ticks.Add(item.Value.N);
                if (item.Value.N == 19)
                {
                    break;
                }
            }
            var expectedTicks = Enumerable.Range(15, 5).ToArray();
            Assert.Equal(expectedTicks.Select(n => (long)n), offsets);
            Assert.Equal(expectedTicks, ticks);
            await handle.SignalAsync(wf => wf.SubscriberCompleteAsync("test"));
            // Repeated acknowledgements from one subscriber must not close the stream.
            await handle.SignalAsync(wf => wf.SubscriberCompleteAsync("test"));
            Assert.Equal(20, await streamClient.GetOffsetAsync());
            Assert.Equal(WorkflowExecutionStatus.Running, (await handle.DescribeAsync()).Status);
            await handle.SignalAsync(wf => wf.SubscriberCompleteAsync("other"));
            Assert.Equal("ticker emitted 20 events", await handle.GetResultAsync());
        });
    }

    private TemporalWorkerOptions NewWorker() =>
        new($"workflow-streams-{Guid.NewGuid()}");
}
