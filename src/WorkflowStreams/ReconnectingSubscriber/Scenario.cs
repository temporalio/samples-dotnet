namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams;

public static partial class Scenarios
{
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
}
