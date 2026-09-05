namespace TemporalioSamples.WorkflowStreams.LlmTokenStreaming;

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using OpenAI;
using OpenAI.Chat;
using Temporalio.Activities;
using Temporalio.Extensions.WorkflowStreams;

public static class LlmActivities
{
    [Activity]
    public static async Task<string> StreamCompletionAsync(LlmInput input)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY must be set for the LLM scenario");
        }

        await using var streamClient = WorkflowStreamClient.FromActivity(
            new() { BatchInterval = TimeSpan.FromMilliseconds(200), });
        var deltas = streamClient.Topic(Constants.TopicDelta);
        var complete = streamClient.Topic(Constants.TopicComplete);
        var retry = streamClient.Topic(Constants.TopicRetry);

        var activityContext = ActivityExecutionContext.Current;
        if (activityContext.Info.Attempt > 1)
        {
            retry.Publish(new RetryEvent(activityContext.Info.Attempt), forceFlush: true);
        }

        var clientOptions = new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(0),
        };
        var chatClient = new ChatClient(
            input.Model,
            new ApiKeyCredential(apiKey),
            clientOptions);

        var fullText = new StringBuilder();
        var updates = chatClient.CompleteChatStreamingAsync(
            [new UserChatMessage(input.Prompt)],
            cancellationToken: activityContext.CancellationToken);
        await foreach (var update in updates)
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (string.IsNullOrEmpty(contentPart.Text))
                {
                    continue;
                }
                deltas.Publish(new TextDelta(contentPart.Text));
                fullText.Append(contentPart.Text);
            }
        }

        var result = fullText.ToString();
        complete.Publish(new TextComplete(result), forceFlush: true);
        await streamClient.FlushAsync();
        return result;
    }
}
