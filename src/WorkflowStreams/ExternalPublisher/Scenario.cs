namespace TemporalioSamples.WorkflowStreams.ExternalPublisher;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;

public static class Scenario
{
    private static readonly string[] Headlines =
    [
        "markets open higher",
        "new bridge opens downtown",
        "local team wins championship",
    ];

    public static async Task RunExternalPublisherAsync(ITemporalClient client)
    {
        var workflowId = $"workflow-streams-hub-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (HubWorkflow wf) => wf.RunAsync(new HubInput("newsroom", null)),
            new(workflowId, Constants.TaskQueue));
        Console.WriteLine($"Started workflow: {workflowId}");

        async Task SubscribeAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            await foreach (var item in streamClient.Topic(Constants.TopicNews).SubscribeAsync())
            {
                var evt = Decode<NewsEvent>(client, item);
                if (evt.Headline == Constants.DoneHeadline)
                {
                    break;
                }
                Console.WriteLine($"[subscriber] {evt.Headline}");
            }
        }

        async Task PublishAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            var news = streamClient.Topic(Constants.TopicNews);
            foreach (var headline in Headlines)
            {
                news.Publish(new NewsEvent(headline));
                Console.WriteLine($"[publisher]  sent: {headline}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            news.Publish(new NewsEvent(Constants.DoneHeadline), forceFlush: true);
            await streamClient.FlushAsync();
            await handle.SignalAsync(wf => wf.CloseAsync());
            Console.WriteLine("[publisher]  signaled close");
        }

        await Task.WhenAll(SubscribeAsync(), PublishAsync());
        Console.WriteLine($"Workflow result: {await handle.GetResultAsync()}");
    }

    private static T Decode<T>(ITemporalClient client, WorkflowStreamItem item) =>
        client.Options.DataConverter.PayloadConverter.ToValue<T>(item.Payload);
}
