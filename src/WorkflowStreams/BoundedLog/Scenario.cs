namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams;

public static partial class Scenarios
{
    private const int TickCount = 30;
    private const int KeepLast = 5;
    private const int TruncateEvery = 5;
    private const long StaleOffset = 1;

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
}
