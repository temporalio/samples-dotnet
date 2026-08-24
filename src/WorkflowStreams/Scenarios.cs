namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;

public static class Scenarios
{
    private const int TickCount = 30;
    private const int KeepLast = 5;
    private const int TruncateEvery = 5;
    private const long StaleOffset = 1;

    private static readonly string[] Headlines =
    [
        "markets open higher",
        "new bridge opens downtown",
        "local team wins championship",
    ];

    private static readonly string[] OrderIds = new[] { "order-A", "order-B" };

    public static async Task RunPublisherAsync(ITemporalClient client)
    {
        var workflowId = $"workflow-streams-order-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderInput("order-42", null)),
            new(workflowId, WorkflowStreamsConstants.TaskQueue));
        Console.WriteLine($"Started workflow: {workflowId}");

        await using var streamClient = new WorkflowStreamClient(client, workflowId);
        var options = new SubscribeOptions
        {
            Topics = new List<string>
            {
                WorkflowStreamsConstants.TopicStatus,
                WorkflowStreamsConstants.TopicProgress,
            },
        };
        await foreach (var item in streamClient.Subscribe(options))
        {
            if (item.Topic == WorkflowStreamsConstants.TopicStatus)
            {
                var evt = Decode<StatusEvent>(client, item);
                Console.WriteLine($"[status]   {evt.Kind}: order={evt.OrderId}");
                if (evt.Kind == "complete")
                {
                    break;
                }
            }
            else if (item.Topic == WorkflowStreamsConstants.TopicProgress)
            {
                var evt = Decode<ProgressEvent>(client, item);
                Console.WriteLine($"[progress] {evt.Message}");
            }
        }

        Console.WriteLine($"Workflow result: {await handle.GetResultAsync()}");
    }

    public static async Task RunListenerAsync(ITemporalClient client)
    {
        var workflowHandles = new List<WorkflowHandle<OrderWorkflow>>();
        var streamClients = new List<WorkflowStreamClient>();
        var subscriptionHandles = new List<WorkflowStreamSubscriptionHandle>();
        using var renderLock = new SemaphoreSlim(1, 1);

        foreach (var orderId in OrderIds)
        {
            var workflowId = $"workflow-streams-listener-{orderId}-{Guid.NewGuid()}";
            var workflowHandle = await client.StartWorkflowAsync(
                (OrderWorkflow wf) => wf.RunAsync(new OrderInput(orderId, null)),
                new(workflowId, WorkflowStreamsConstants.TaskQueue));
            Console.WriteLine($"Started workflow: {workflowId}");

#pragma warning disable CA2000 // The client is closed in the method's finally block
            var streamClient = new WorkflowStreamClient(client, workflowId);
#pragma warning restore CA2000
            var listener = new RenderingListener(client, orderId, renderLock);
            var subscriptionHandle = streamClient.Subscribe(
                new SubscribeOptions
                {
                    Topics = new List<string>
                    {
                        WorkflowStreamsConstants.TopicStatus,
                        WorkflowStreamsConstants.TopicProgress,
                    },
                },
                listener);
            workflowHandles.Add(workflowHandle);
            streamClients.Add(streamClient);
            subscriptionHandles.Add(subscriptionHandle);
        }

        try
        {
            await Task.WhenAll(subscriptionHandles.Select(handle => handle.Completion));
            for (var i = 0; i < workflowHandles.Count; i++)
            {
                var result = await workflowHandles[i].GetResultAsync<string>();
                Console.WriteLine($"[{OrderIds[i]}] workflow result: {result}");
            }
        }
        finally
        {
            foreach (var handle in subscriptionHandles)
            {
                handle.Dispose();
            }
            foreach (var streamClient in streamClients)
            {
                await streamClient.CloseAsync();
            }
        }
    }

    public static async Task RunReconnectingSubscriberAsync(ITemporalClient client)
    {
        var workflowId = $"workflow-streams-pipeline-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (PipelineWorkflow wf) => wf.RunAsync(new PipelineInput("pipeline-7", null, null)),
            new(workflowId, WorkflowStreamsConstants.TaskQueue));
        Console.WriteLine($"Started workflow: {workflowId}");

        long nextOffset = 0;
        Console.WriteLine("--- phase 1: initial subscriber ---");
        await using (var streamClient = new WorkflowStreamClient(client, workflowId))
        {
            var seen = 0;
            await foreach (var item in streamClient.Topic(WorkflowStreamsConstants.TopicStatus).Subscribe())
            {
                var evt = Decode<StageEvent>(client, item);
                nextOffset = item.Offset + 1;
                Console.WriteLine($"offset={item.Offset}  stage={evt.Stage}");
                if (++seen == 2)
                {
                    break;
                }
            }
        }

        Console.WriteLine($"--- disconnected; will resume from offset {nextOffset} ---");
        Console.WriteLine("--- phase 2: reconnected subscriber ---");
        await using (var streamClient = new WorkflowStreamClient(client, workflowId))
        {
            await foreach (var item in streamClient.Topic(WorkflowStreamsConstants.TopicStatus).Subscribe(nextOffset))
            {
                var evt = Decode<StageEvent>(client, item);
                Console.WriteLine($"offset={item.Offset}  stage={evt.Stage}");
                if (evt.Stage == "complete")
                {
                    break;
                }
            }
        }

        Console.WriteLine($"Workflow result: {await handle.GetResultAsync()}");
    }

    public static async Task RunExternalPublisherAsync(ITemporalClient client)
    {
        var workflowId = $"workflow-streams-hub-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (HubWorkflow wf) => wf.RunAsync(new HubInput("newsroom", null)),
            new(workflowId, WorkflowStreamsConstants.TaskQueue));
        Console.WriteLine($"Started workflow: {workflowId}");

        async Task SubscribeAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            await foreach (var item in streamClient.Topic(WorkflowStreamsConstants.TopicNews).Subscribe())
            {
                var evt = Decode<NewsEvent>(client, item);
                if (evt.Headline == WorkflowStreamsConstants.DoneHeadline)
                {
                    break;
                }
                Console.WriteLine($"[subscriber] {evt.Headline}");
            }
        }

        async Task PublishAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            var news = streamClient.Topic(WorkflowStreamsConstants.TopicNews);
            foreach (var headline in Headlines)
            {
                news.Publish(new NewsEvent(headline));
                Console.WriteLine($"[publisher]  sent: {headline}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            news.Publish(new NewsEvent(WorkflowStreamsConstants.DoneHeadline), forceFlush: true);
            await streamClient.FlushAsync();
            await handle.SignalAsync(wf => wf.CloseAsync());
            Console.WriteLine("[publisher]  signaled close");
        }

        await Task.WhenAll(SubscribeAsync(), PublishAsync());
        Console.WriteLine($"Workflow result: {await handle.GetResultAsync()}");
    }

    public static async Task RunTruncatingTickerAsync(ITemporalClient client)
    {
        var workflowId = $"workflow-streams-ticker-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (TickerWorkflow wf) => wf.RunAsync(
                new TickerInput(TickCount, KeepLast, TruncateEvery, null, null)),
            new(workflowId, WorkflowStreamsConstants.TaskQueue));
        Console.WriteLine($"Started workflow: {workflowId}");

        async Task FastSubscriberAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            await foreach (var item in streamClient.Topic(WorkflowStreamsConstants.TopicTick).Subscribe())
            {
                var evt = Decode<TickEvent>(client, item);
                Console.WriteLine($"[fast] offset={item.Offset,3}  n={evt.N}");
                if (evt.N == TickCount - 1)
                {
                    break;
                }
            }
        }

        async Task LateSubscriberAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            var firstTruncate = ((KeepLast / TruncateEvery) + 1) * TruncateEvery;
            while (await streamClient.GetOffsetAsync() <= firstTruncate)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            var first = true;
            await foreach (var item in streamClient.Topic(WorkflowStreamsConstants.TopicTick).Subscribe(StaleOffset))
            {
                var evt = Decode<TickEvent>(client, item);
                if (first && item.Offset > StaleOffset)
                {
                    Console.WriteLine(
                        $"[late] requested offset {StaleOffset} but it was truncated; " +
                        $"fast-forwarded to offset {item.Offset} " +
                        $"(skipped {item.Offset - StaleOffset} tick(s))");
                }
                first = false;
                Console.WriteLine($"[late] offset={item.Offset,3}  n={evt.N}");
                if (evt.N == TickCount - 1)
                {
                    break;
                }
            }
        }

        await Task.WhenAll(FastSubscriberAsync(), LateSubscriberAsync());
        Console.WriteLine($"Workflow result: {await handle.GetResultAsync()}");
    }

    public static async Task RunLlmAsync(ITemporalClient client, string? prompt)
    {
        const string ansiSave = "\u001b[s";
        const string ansiRestoreAndClear = "\u001b[u\u001b[J";
        prompt ??=
            "Write a 500-word comparison of Paxos, Raft, and Viewstamped Replication for " +
            "a new distributed-systems engineer. Cover the core ideas, leader election, " +
            "normal-case operation, reconfiguration, and practical implementation tradeoffs.";

        var input = new LlmInput(prompt);
        var workflowId = $"workflow-streams-llm-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (LlmWorkflow wf) => wf.RunAsync(input),
            new(workflowId, WorkflowStreamsConstants.LlmTaskQueue));
        Console.WriteLine(
            $"[llm {workflowId}] streaming response from {input.Model}, awaiting first token...");
        Console.WriteLine();
        Console.Write(ansiSave);

        await using var streamClient = new WorkflowStreamClient(client, workflowId);
        var options = new SubscribeOptions
        {
            Topics = new List<string>
            {
                WorkflowStreamsConstants.TopicDelta,
                WorkflowStreamsConstants.TopicRetry,
                WorkflowStreamsConstants.TopicComplete,
            },
        };
        await foreach (var item in streamClient.Subscribe(options))
        {
            if (item.Topic == WorkflowStreamsConstants.TopicRetry)
            {
                var evt = Decode<RetryEvent>(client, item);
                Console.Write(ansiRestoreAndClear);
                Console.WriteLine($"[retry attempt {evt.Attempt}] resetting output");
                Console.WriteLine();
                Console.Write(ansiSave);
            }
            else if (item.Topic == WorkflowStreamsConstants.TopicDelta)
            {
                Console.Write(Decode<TextDelta>(client, item).Text);
            }
            else if (item.Topic == WorkflowStreamsConstants.TopicComplete)
            {
                _ = Decode<TextComplete>(client, item);
                Console.WriteLine();
                break;
            }
        }

        var result = await handle.GetResultAsync();
        Console.WriteLine($"[workflow result: {result.Length} chars]");
    }

    private static T Decode<T>(ITemporalClient client, WorkflowStreamItem item) =>
        client.Options.DataConverter.PayloadConverter.ToValue<T>(item.Payload);

    private sealed class RenderingListener(
        ITemporalClient client,
        string orderId,
        SemaphoreSlim renderLock) : WorkflowStreamListener
    {
        public override async Task OnNextAsync(WorkflowStreamItem item)
        {
            await renderLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (item.Topic == WorkflowStreamsConstants.TopicStatus)
                {
                    Console.WriteLine(
                        $"[{orderId}] [status]   {Decode<StatusEvent>(client, item).Kind}");
                }
                else if (item.Topic == WorkflowStreamsConstants.TopicProgress)
                {
                    Console.WriteLine(
                        $"[{orderId}] [progress] {Decode<ProgressEvent>(client, item).Message}");
                }
            }
            finally
            {
                renderLock.Release();
            }
        }

        public override void OnCompleted() =>
            Console.WriteLine($"[{orderId}] stream completed");

        public override void OnError(Exception failure) =>
            Console.Error.WriteLine($"[{orderId}] stream failed: {failure}");
    }
}
