namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Microsoft.Extensions.AI;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.ContinueAsNew;
using Xunit;
using Xunit.Abstractions;

public class ContinueAsNewWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    // A low threshold makes the Temporal Service suggest Continue-As-New after the first turn.
    private static readonly string[] DevServerArgs = new[] { "--dynamic-config-value", "limit.historyCount.suggestContinueAsNew=5" };

    [Fact]
    public async Task TurnAsync_ContinuesAsNew_SessionCarriesToTheNewRun()
    {
        // Echoes the latest user message with the count of user messages seen, so the reply proves
        // how much history the model received.
        using var fake = new FakeChatClient(call =>
        {
            var users = call.Messages.Where(message => message.Role == ChatRole.User).ToList();
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, $"echo[{users.Count}]: {users[^1].Text}"));
        });
        await using var env = await WorkflowEnvironment.StartLocalAsync(new()
        {
            LoggerFactory = LoggerFactory,
            DevServerOptions = new()
            {
                ExtraArgs = DevServerArgs,
            },
        });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("chat", TestSupport.PluginFor(fake)).AddWorkflow<ChatWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Each turn is an Update-With-Start on a fixed id, the shape the sample's send-message verb uses:
            // the first one starts the chat, later ones join the running Workflow.
            var options = TestSupport.WorkflowOptions(worker, "chat");
            options.IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting;
            var handle = await TurnAsync("first", options, "echo[1]: first");

            await AssertMore.EventuallyAsync(async () =>
            {
                var firstRun = await (handle with { RunId = handle.ResultRunId }).FetchHistoryAsync();
                Assert.Contains(firstRun.Events, evt => evt.WorkflowExecutionContinuedAsNewEventAttributes is not null);
            });

            // The second turn lands on the new run, which restored the session: the model sees both turns.
            await TurnAsync("second", options, "echo[2]: second");
            Assert.Equal(3, fake.Calls[1].Messages.Count);
            await AssertMore.EventuallyAsync(async () =>
                Assert.Equal(4, (await handle.QueryAsync(wf => wf.Transcript())).Count));

            await handle.SignalAsync(wf => wf.EndChatAsync());
            await handle.GetResultAsync();
        });

        async Task<WorkflowHandle<ChatWorkflow>> TurnAsync(string prompt, WorkflowOptions options, string expectedReply)
        {
            var startOperation = WithStartWorkflowOperation.Create((ChatWorkflow wf) => wf.RunAsync(new ChatInput(null)), options);
            var reply = await env.Client.ExecuteUpdateWithStartWorkflowAsync((ChatWorkflow wf) => wf.TurnAsync(prompt), new(startOperation));
            Assert.Equal(expectedReply, reply);
            return await startOperation.GetHandleAsync();
        }
    }
}
