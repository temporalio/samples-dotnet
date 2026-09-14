namespace TemporalioSamples.WorkflowStreams.ConcurrentSubscriptions;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;

public static class Scenario
{
    private static readonly string[] OrderIds = new[] { "order-A", "order-B" };

    public static async Task RunSubscriptionsAsync(ITemporalClient client)
    {
        var workflowHandles = new List<WorkflowHandle<OrderWorkflow>>();
        var streamClients = new List<WorkflowStreamClient>();
        var subscriptions = new List<Task>();

        foreach (var orderId in OrderIds)
        {
            var workflowId = $"workflow-streams-concurrent-{orderId}-{Guid.NewGuid()}";
            var workflowHandle = await client.StartWorkflowAsync(
                (OrderWorkflow wf) => wf.RunAsync(new OrderInput(orderId, null)),
                new(workflowId, Constants.TaskQueue));
            Console.WriteLine($"Started workflow: {workflowId}");

#pragma warning disable CA2000 // The client is closed in the method's finally block
            var streamClient = new WorkflowStreamClient(client, workflowId);
#pragma warning restore CA2000
            workflowHandles.Add(workflowHandle);
            streamClients.Add(streamClient);
            subscriptions.Add(RenderSubscriptionAsync(streamClient, client, orderId));
        }

        try
        {
            await Task.WhenAll(subscriptions);
            for (var i = 0; i < workflowHandles.Count; i++)
            {
                var result = await workflowHandles[i].GetResultAsync<string>();
                Console.WriteLine($"[{OrderIds[i]}] workflow result: {result}");
            }
        }
        finally
        {
            foreach (var streamClient in streamClients)
            {
                await streamClient.DisposeAsync();
            }
        }
    }

    private static async Task RenderSubscriptionAsync(
        WorkflowStreamClient streamClient,
        ITemporalClient client,
        string orderId)
    {
        await foreach (var item in streamClient.SubscribeAsync(new()
        {
            Topics = new List<string>
            {
                Constants.TopicStatus,
                Constants.TopicProgress,
            },
        }))
        {
            if (item.Topic == Constants.TopicStatus)
            {
                Console.WriteLine(
                    $"[{orderId}] [status]   {Decode<StatusEvent>(client, item).Kind}");
            }
            else if (item.Topic == Constants.TopicProgress)
            {
                Console.WriteLine(
                    $"[{orderId}] [progress] {Decode<ProgressEvent>(client, item).Message}");
            }
        }
        Console.WriteLine($"[{orderId}] stream completed");
    }

    private static T Decode<T>(ITemporalClient client, WorkflowStreamItem item) =>
        client.Options.DataConverter.PayloadConverter.ToValue<T>(item.Payload);
}
