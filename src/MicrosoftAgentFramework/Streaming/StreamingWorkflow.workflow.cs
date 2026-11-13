// @@@SNIPSTART dotnet-microsoft-agent-framework-streaming-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.Streaming;

using Microsoft.Extensions.AI;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Workflows;

public record StreamingInput(string Prompt, WorkflowStreamState? StreamState = null);

[Workflow]
public class StreamingWorkflow
{
    public const string Topic = "deltas";

    // Constructing the stream registers its publish Signal, poll Update and offset Query before
    // the model Activity publishes to it.
#pragma warning disable IDE0052 // The stream is used through its registered handlers.
    private readonly WorkflowStream stream;
#pragma warning restore IDE0052

    [WorkflowInit]
    public StreamingWorkflow(StreamingInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(StreamingInput input)
    {
        // With a topic set, the model Activity consumes the provider's stream and publishes each
        // ChatResponseUpdate, batched every 100 milliseconds. The Workflow still gets the whole
        // response, so the durable result is the same as without streaming.
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            StreamingTopic = Topic,
            ChatOptions = new ChatOptions { Instructions = "You are a concise assistant." },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        var response = await agent.RunAsync(input.Prompt);
        // Poll Updates cannot read the stream after the Workflow closes. This delay gives active
        // subscribers time to fetch the last batch.
        await Workflow.DelayAsync(TimeSpan.FromMilliseconds(500));
        return response.Text;
    }
}
// @@@SNIPEND
