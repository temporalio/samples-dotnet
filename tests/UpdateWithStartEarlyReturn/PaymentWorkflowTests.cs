namespace TemporalioSamples.Tests.UpdateWithStartEarlyReturn;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.UpdateWithStartEarlyReturn;
using Xunit;
using Xunit.Abstractions;

public class PaymentWorkflowTests : WorkflowEnvironmentTestBase
{
    public PaymentWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_Simple_GoesInOrder()
    {
        // Mock activities
        [Activity]
        Task AuthorizePaymentAsync(int amount) => Task.CompletedTask;

        var captureStartedSource = new TaskCompletionSource();
        var captureCompleteSource = new TaskCompletionSource();

        [Activity]
        async Task CapturePaymentAsync()
        {
            captureStartedSource.SetResult();
            await captureCompleteSource.Task;
        }

        // Run worker
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue: $"tq-{Guid.NewGuid()}").
                AddActivity(AuthorizePaymentAsync).
                AddActivity(CapturePaymentAsync).
                AddWorkflow<PaymentWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Issue payment, confirm via history update completed after auth but before capture
            var startOperation = WithStartWorkflowOperation.Create(
                (PaymentWorkflow wf) => wf.RunAsync(new(123)),
                new($"wf-{Guid.NewGuid()}", worker.Options.TaskQueue!)
                {
                    IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.Fail,
                });
            // Wait for update complete, then confirm authorized and capture started
            await Client.ExecuteUpdateWithStartWorkflowAsync(
                (PaymentWorkflow wf) => wf.WaitUntilAuthorizedAsync(),
                new(startOperation));
            var handle = await startOperation.GetHandleAsync();
            await captureStartedSource.Task;
            Assert.True(await handle.QueryAsync(wf => wf.Authorized));

            // Now complete capture
            captureCompleteSource.SetResult();
            await handle.GetResultAsync();
        });
    }
}