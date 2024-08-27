using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Bedrock.Entity;

[Workflow]
public class BedrockWorkflow
{
    public record BedrockWorkflowArgs(string? ConversationSummary = null, Queue<string>? PromptQueue = null, bool? ChatEnded = null);

    public record BedrockWorkflowResult(Collection<ConversationEntry> ConversationHistory);

    public record BedrockUserPromptSignal(string Prompt);

    public record ConversationEntry(string Speaker, string Message);

    private const int ContinueAsNewPerTurns = 6;
    private readonly Queue<string> promptQueue = new();
    private bool chatEnded;

    [WorkflowRun]
    public async Task<BedrockWorkflowResult> RunAsync(BedrockWorkflowArgs args)
    {
        if (args.ConversationSummary is not null)
        {
            ConversationHistory.Add(new ConversationEntry(Speaker: "conversation_summary", Message: args.ConversationSummary));
            ConversationSummary = args.ConversationSummary;
        }

        if (args.PromptQueue is not null)
        {
            while (args.PromptQueue.TryDequeue(out var prompt))
            {
                promptQueue.Enqueue(prompt);
            }
        }

        if (args.ChatEnded is not null)
        {
            chatEnded = args.ChatEnded.Value;
        }

        while (true)
        {
            Workflow.Logger.LogInformation("Waiting for prompts...");

            // Wait for a chat message or chat ended signal
            await Workflow.WaitConditionAsync(() => promptQueue.Count > 0 || chatEnded);

            // Fetch next user prompt and add to conversation history
            while (promptQueue.TryDequeue(out var prompt))
            {
                ConversationHistory.Add(new(Speaker: "user", Message: prompt));
                Workflow.Logger.LogInformation("Prompt: {Prompt}", prompt);

                // Send the prompt to Amazon Bedrock
                var promptResult = await Workflow.ExecuteActivityAsync(
                    (BedrockActivities activities) => activities.PromptBedrockAsync(new(PromptWithHistory(prompt))),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromSeconds(20),
                    });

                Workflow.Logger.LogInformation("Response:\n{Response}", promptResult.Response);

                // Append the response to the conversation history
                ConversationHistory.Add(new(Speaker: "response", promptResult.Response));

                // Continue as new every x conversational turns to avoid event
                // history size getting too large. This is also to avoid the
                // prompt (with conversational history) getting too large for
                // AWS Bedrock.

                // We summarize the chat to date and use that as input to the
                // new workflow
                if (ConversationHistory.Count >= ContinueAsNewPerTurns)
                {
                    // Summarize the conversation to date using Amazon Bedrock
                    var summaryResult = await Workflow.ExecuteActivityAsync(
                        (BedrockActivities activities) => activities.PromptBedrockAsync(new(PromptSummaryFromHistory())),
                        new()
                        {
                            StartToCloseTimeout = TimeSpan.FromSeconds(20),
                        });

                    ConversationSummary = summaryResult.Response;
                    Workflow.Logger.LogInformation("Continuing as new due to {ContinueAsNewPerTurns} conversational turns.", ContinueAsNewPerTurns);

                    throw Workflow.CreateContinueAsNewException<BedrockWorkflow>(workflow =>
                        workflow.RunAsync(new(ConversationSummary, promptQueue, chatEnded)));
                }
            }

            // If end chat signal was sent
            if (chatEnded)
            {
                // The workflow might be continued as new without any
                // chat to summarize, so only call Bedrock if there
                // is more than the previous summary in the history.
                if (ConversationHistory.Count > 1)
                {
                    var summaryResult = await Workflow.ExecuteActivityAsync(
                        (BedrockActivities activities) => activities.PromptBedrockAsync(new(PromptSummaryFromHistory())),
                        new()
                        {
                            StartToCloseTimeout = TimeSpan.FromSeconds(20),
                        });

                    ConversationSummary = summaryResult.Response;
                }

                Workflow.Logger.LogInformation("Chat ended. Conversation summary:\n{ConversationSummary}", ConversationSummary);
                return new BedrockWorkflowResult(ConversationHistory);
            }
        }
    }

    [WorkflowQuery]
    public string? ConversationSummary { get; private set; }

    [WorkflowQuery]
    public Collection<ConversationEntry> ConversationHistory { get; } = new();

    [WorkflowSignal]
    public Task UserPromptAsync(BedrockUserPromptSignal signal)
    {
        // Chat timed out but the workflow is waiting for a chat summary to be generated
        if (chatEnded)
        {
            Workflow.Logger.LogWarning("Message dropped due to chat closed: {Prompt}", signal.Prompt);
            return Task.CompletedTask;
        }

        promptQueue.Enqueue(signal.Prompt);
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public async Task EndChatAsync() => chatEnded = true;

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