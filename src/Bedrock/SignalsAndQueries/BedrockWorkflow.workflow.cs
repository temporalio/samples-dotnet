using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Bedrock.SignalsAndQueries;

public record BedrockWorkflowArgs(int InactivityTimeoutMinutes);
public record BedrockWorkflowResult(Collection<ConversationEntry> ConversationHistory);
public record BedrockUserPromptSignal(string Prompt);

public record ConversationEntry(string Speaker, string Message);

[Workflow]
public class BedrockWorkflow
{
    private readonly Queue<string> promptQueue = new();
    private bool chatTimeout;

    [WorkflowRun]
    public async Task<BedrockWorkflowResult> RunAsync(BedrockWorkflowArgs args)
    {
        while (true)
        {
            Workflow.Logger.LogInformation("Waiting for prompts... or closing chat after {InactivityTimeoutMinutes} minutes(s)", args.InactivityTimeoutMinutes);

            // Wait for a chat message (signal) or timeout
            if (await Workflow.WaitConditionAsync(() => promptQueue.Count > 0, timeout: TimeSpan.FromMinutes(args.InactivityTimeoutMinutes)))
            {
                // Fetch next user prompt and add to conversation history
                while (promptQueue.TryDequeue(out var prompt))
                {
                    ConversationHistory.Add(new(Speaker: "user", prompt));
                    Workflow.Logger.LogInformation("Prompt: {Prompt}", prompt);

                    // Send the prompt to Amazon Bedrock
                    var promptResult = await Workflow.ExecuteActivityAsync(
                        (IBedrockActivities activities) => activities.PromptBedrockAsync(new(PromptWithHistory(prompt))),
                        new()
                        {
                            StartToCloseTimeout = TimeSpan.FromSeconds(20),
                        });

                    Workflow.Logger.LogInformation("Response:\n{Response}", promptResult.Response);

                    // Append the response to the conversation history
                    ConversationHistory.Add(new(Speaker: "response", promptResult.Response));
                }
            }
            else
            {
                // If timeout was reached
                chatTimeout = true;
                Workflow.Logger.LogInformation("Chat closed due to inactivity");

                // End the workflow
                break;
            }
        }

        // Generate a summary before ending the workflow
        var summaryResult = await Workflow.ExecuteActivityAsync(
            (IBedrockActivities activities) => activities.PromptBedrockAsync(new(PromptSummaryFromHistory())),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(20),
            });

        ConversationSummary = summaryResult.Response;
        Workflow.Logger.LogInformation("Conversation summary:\n{ConversationSummary}", ConversationSummary);
        return new BedrockWorkflowResult(ConversationHistory);
    }

    [WorkflowQuery]
    public string? ConversationSummary { get; private set; }

    [WorkflowQuery]
    public Collection<ConversationEntry> ConversationHistory { get; } = new();

    [WorkflowSignal]
    public Task UserPromptAsync(BedrockUserPromptSignal signal)
    {
        // Chat timed out but the workflow is waiting for a chat summary to be generated
        if (chatTimeout)
        {
            Workflow.Logger.LogWarning("Message dropped due to chat closed: {Prompt}", signal.Prompt);
            return Task.CompletedTask;
        }

        promptQueue.Enqueue(signal.Prompt);
        return Task.CompletedTask;
    }

    private string FormatHistory() => string.Join(" ", ConversationHistory.Select(x => x.Message));

    private string PromptWithHistory(string prompt)
    {
        // Create the prompt given to Amazon Bedrock for each conversational turn
        var history = FormatHistory();

        return $"""
                Here is the conversation history: {history} Please add
                a few sentence response to the prompt in plain text sentences.
                Don't editorialize or add metadata like response. Keep the
                text a plain explanation based on the history. Prompt: {prompt}
                """;
    }

    private string PromptSummaryFromHistory()
    {
        // Create the prompt to Amazon Bedrock to summarize the conversation history
        var history = FormatHistory();

        return $"""
                Here is the conversation history between a user and a chatbot: 
                {history}  -- Please produce a two sentence summary of
                this conversation. 
                """;
    }
}