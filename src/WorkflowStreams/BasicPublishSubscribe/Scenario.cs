namespace TemporalioSamples.WorkflowStreams.BasicPublishSubscribe;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;

public static class Scenario
{
    public static async Task RunPublisherAsync(ITemporalClient client)
    {
        var workflowId = $"workflow-streams-order-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderInput("order-42", null)),
            new(workflowId, Constants.TaskQueue));
        Console.WriteLine($"Started workflow: {workflowId}");

        await using var streamClient = new WorkflowStreamClient(client, workflowId);

        // This sample uses the default payload converter and no payload codec. Since the
        // subscription combines different event types, decode each item with the payload
        // converter bound to the target Workflow's namespace and ID. Typed subscriptions
        // perform this conversion automatically.
        //
        // Workflow Streams applies payload conversion to each item. A configured codec,
        // including encryption, applies to the enclosing Signal or Update payload, so do
        // not run the codec again when decoding an item. Publishers, the Workflow host,
        // and subscribers must use compatible converters and codecs. Cross-language
        // envelopes require JSON-compatible conversion.
        //
        // FromActivity publishes with the Activity's payload converter and context,
        // while subscribers decode in a Workflow context. A custom converter requiring
        // matching contexts for serialization and deserialization is incompatible with
        // Activity publications consumed by these subscribers.
        var payloadConverter = client.Options.DataConverter.WithSerializationContext(
            new ISerializationContext.Workflow(client.Options.Namespace, workflowId)).PayloadConverter;
        var options = new WorkflowStreamSubscribeOptions
        {
            Topics = new List<string>
            {
                Constants.TopicStatus,
                Constants.TopicProgress,
            },
        };
        await foreach (var item in streamClient.SubscribeAsync(options))
        {
            if (item.Topic == Constants.TopicStatus)
            {
                var evt = payloadConverter.ToValue<StatusEvent>(item.Payload);
                Console.WriteLine($"[status]   {evt.Kind}: order={evt.OrderId}");
                if (evt.Kind == "complete")
                {
                    break;
                }
            }
            else if (item.Topic == Constants.TopicProgress)
            {
                var evt = payloadConverter.ToValue<ProgressEvent>(item.Payload);
                Console.WriteLine($"[progress] {evt.Message}");
            }
        }

        Console.WriteLine($"Workflow result: {await handle.GetResultAsync()}");
    }
}
