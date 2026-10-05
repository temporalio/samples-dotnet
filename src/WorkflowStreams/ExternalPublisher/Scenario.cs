namespace TemporalioSamples.WorkflowStreams.ExternalPublisher;

using Temporalio.Client;
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
        var initialRunId = handle.FirstExecutionRunId;
        Console.WriteLine($"Started workflow: {workflowId}");

        async Task SubscribeAsync()
        {
            await using var streamClient = new WorkflowStreamClient(client, workflowId);
            await foreach (var item in streamClient.
                GetTopic<NewsEvent>(Constants.TopicNews).SubscribeAsync())
            {
                var evt = item.Value;
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
            var news = streamClient.GetTopic<NewsEvent>(Constants.TopicNews);
            for (var index = 0; index < Headlines.Length; index++)
            {
                var headline = Headlines[index];
                news.Publish(new NewsEvent(headline));
                Console.WriteLine($"[publisher]  sent: {headline}");
                if (index == 0)
                {
                    // Flush the first headline into this run, then wait for the successor run
                    // before publishing more. This demonstrates that the existing publisher
                    // and subscriber follow Continue-As-New while preserving stream offsets.
                    await streamClient.FlushAsync();
                    await handle.SignalAsync(wf => wf.ContinueAsNewAsync());
                    while ((await handle.DescribeAsync()).RunId == initialRunId)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                    }
                    Console.WriteLine("[publisher]  workflow continued as new");
                }
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
}
