namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams;

public static partial class Scenarios
{
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
}
