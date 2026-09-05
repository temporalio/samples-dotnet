namespace TemporalioSamples.WorkflowStreams.LlmTokenStreaming;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;

public static class Scenario
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
            new(workflowId, Constants.TaskQueue));
        Console.WriteLine(
            $"[llm {workflowId}] streaming response from {input.Model}, awaiting first token...");
        Console.WriteLine();
        Console.Write(ansiSave);

        await using var streamClient = new WorkflowStreamClient(client, workflowId);
        var options = new WorkflowStreamSubscribeOptions
        {
            Topics = new List<string>
            {
                Constants.TopicDelta,
                Constants.TopicRetry,
                Constants.TopicComplete,
            },
        };
        await foreach (var item in streamClient.SubscribeAsync(options))
        {
            if (item.Topic == Constants.TopicRetry)
            {
                var evt = Decode<RetryEvent>(client, item);
                Console.Write(ansiRestoreAndClear);
                Console.WriteLine($"[retry attempt {evt.Attempt}] resetting output");
                Console.WriteLine();
                Console.Write(ansiSave);
            }
            else if (item.Topic == Constants.TopicDelta)
            {
                Console.Write(Decode<TextDelta>(client, item).Text);
            }
            else if (item.Topic == Constants.TopicComplete)
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
}
