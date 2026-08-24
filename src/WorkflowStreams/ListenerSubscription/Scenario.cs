namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams;

public static partial class Scenarios
{
    private static readonly string[] OrderIds = new[] { "order-A", "order-B" };

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
            var subscriptionHandle = streamClient.Subscribe(
                new SubscribeOptions
                {
                    Topics = new List<string>
                    {
                        WorkflowStreamsConstants.TopicStatus,
                        WorkflowStreamsConstants.TopicProgress,
                    },
                },
                new RenderingListener(client, orderId, renderLock));
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
