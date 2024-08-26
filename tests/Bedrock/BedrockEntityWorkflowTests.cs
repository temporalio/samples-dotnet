namespace TemporalioSamples.Tests.Bedrock;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.Bedrock.Entity;
using Xunit;
using Xunit.Abstractions;

public class BedrockEntityWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task ContinuesAsNew()
    {
        var activities = new TestBedrockActivities();

        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("bedrock-entity-test-task-queue").
                AddActivity(activities.PromptBedrockAsync).
                AddWorkflow<BedrockWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var workflowOptions = new WorkflowOptions(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!);
            workflowOptions.SignalWithStart((BedrockWorkflow wf) => wf.UserPromptAsync(new("What animals are marsupials?")));
            var handle = await env.Client.StartWorkflowAsync((BedrockWorkflow wf) => wf.RunAsync(new(null, null, null)), workflowOptions);

            await handle.SignalAsync(wf => wf.UserPromptAsync(new("Do they lay eggs?")));
            await handle.SignalAsync(wf => wf.UserPromptAsync(new("Are you a chicken?")));
            await handle.SignalAsync(wf => wf.EndChatAsync());
            await handle.GetResultAsync();

            // Check whether the workflow continued as new
            var firstRunHistory = await (handle with { RunId = handle.ResultRunId }).FetchHistoryAsync();
            var continued = firstRunHistory.Events.Any(evt => evt.WorkflowExecutionContinuedAsNewEventAttributes != null);
            Assert.True(continued);
        });
    }

    [Fact]
    public async Task QueryConversationHistory()
    {
        var activities = new TestBedrockActivities();

        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("bedrock-entity-test-task-queue").
                AddActivity(activities.PromptBedrockAsync).
                AddWorkflow<BedrockWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"workflow-{Guid.NewGuid()}";
            var workflowOptions = new WorkflowOptions(id: workflowId, taskQueue: worker.Options.TaskQueue!);
            workflowOptions.SignalWithStart((BedrockWorkflow wf) => wf.UserPromptAsync(new("What animals are marsupials?")));

            var handle = await env.Client.StartWorkflowAsync((BedrockWorkflow wf) => wf.RunAsync(new(null, null, null)), workflowOptions);

            await AssertMore.EventuallyAsync(async () =>
            {
                var history = await handle.QueryAsync(workflow => workflow.ConversationHistory);
                Assert.Equal(2, history.Count);
                Assert.Equal(new BedrockWorkflow.ConversationEntry("user", "What animals are marsupials?"), history[0]);
                Assert.Equal(new BedrockWorkflow.ConversationEntry("response", "Marsupials are a group of mammals that give birth to underdeveloped young."), history[1]);
            });

            await handle.SignalAsync(wf => wf.UserPromptAsync(new("Do they lay eggs?")));

            await AssertMore.EventuallyAsync(async () =>
            {
                var history = await handle.QueryAsync(workflow => workflow.ConversationHistory);
                Assert.Equal(4, history.Count);
                Assert.Equal(new BedrockWorkflow.ConversationEntry("user", "What animals are marsupials?"), history[0]);
                Assert.Equal(new BedrockWorkflow.ConversationEntry("response", "Marsupials are a group of mammals that give birth to underdeveloped young."), history[1]);
                Assert.Equal(new BedrockWorkflow.ConversationEntry("user", "Do they lay eggs?"), history[2]);
                Assert.Equal(new BedrockWorkflow.ConversationEntry("response", "No, marsupials do not lay eggs. They are mammals, which means they give birth to live young."), history[3]);
            });

            // At this point the workflow will continue as new.
            await handle.SignalAsync(wf => wf.UserPromptAsync(new("Are you a chicken?")));

            await AssertMore.EventuallyAsync(async () =>
            {
                try
                {
                    var history = await handle.QueryAsync(workflow => workflow.ConversationHistory);
                    Assert.Single(history);
                    Assert.Equal(new BedrockWorkflow.ConversationEntry("conversation_summary", "This is the summary."), history[0]);
                }
                catch (RpcException rpcEx) when (rpcEx.Message == "Workflow task is not scheduled yet.")
                {
                    Assert.Fail("Should not get here");
                }
            });
            await handle.SignalAsync(wf => wf.EndChatAsync());

            await AssertMore.EventuallyAsync(async () =>
            {
                var summary = await handle.QueryAsync(workflow => workflow.ConversationSummary);
                Assert.Equal("This is the summary.", summary);
            });

            await handle.GetResultAsync();

            // Check whether the workflow continued as new
            var firstRunHistory = await (handle with { RunId = handle.ResultRunId }).FetchHistoryAsync();
            var continued = firstRunHistory.Events.Any(evt => evt.WorkflowExecutionContinuedAsNewEventAttributes != null);
            Assert.True(continued);
        });
    }

    private class TestBedrockActivities
    {
        private int count;

        [Activity]
        public Task<BedrockActivities.PromptBedrockActivityResult> PromptBedrockAsync(BedrockActivities.PromptBedrockActivityArgs args)
        {
            count++;
            var result = count switch {
                1 => new BedrockActivities.PromptBedrockActivityResult(
                    "Marsupials are a group of mammals that give birth to underdeveloped young."),
                2 => new BedrockActivities.PromptBedrockActivityResult(
                    "No, marsupials do not lay eggs. They are mammals, which means they give birth to live young."),
                3 => new BedrockActivities.PromptBedrockActivityResult("No, I am not a chicken."),
                4 => new BedrockActivities.PromptBedrockActivityResult("This is the summary."),
                _ => throw new InvalidOperationException("Should not get called"),
            };
            return Task.FromResult(result);
        }
    }
}