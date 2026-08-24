namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams;

public static partial class Scenarios
{
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
}
