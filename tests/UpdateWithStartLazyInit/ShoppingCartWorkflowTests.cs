namespace TemporalioSamples.Tests.UpdateWithStartLazyInit;

using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.UpdateWithStartLazyInit;
using Xunit;
using Xunit.Abstractions;

public class ShoppingCartWorkflowTests : WorkflowEnvironmentTestBase
{
    public ShoppingCartWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_Simple_Succeeds()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue: $"tq-{Guid.NewGuid()}").
                AddAllActivities(typeof(Activities), null).
                AddWorkflow<ShoppingCartWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"wf-{Guid.NewGuid()}";

            async Task<(WorkflowHandle<ShoppingCartWorkflow>, decimal)> AddItemAsync(ShoppingCartItem item)
            {
                var startOperation = WithStartWorkflowOperation.Create(
                    (ShoppingCartWorkflow wf) => wf.RunAsync(),
                    new(workflowId, worker.Options.TaskQueue!)
                    {
                        IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
                    });
                var subtotal = await Client.ExecuteUpdateWithStartWorkflowAsync(
                    (ShoppingCartWorkflow wf) => wf.AddItemAsync(item),
                    new(startOperation));
                return (await startOperation.GetHandleAsync(), subtotal);
            }

            // Add an item
            var (handle, subtotal) = await AddItemAsync(new("sku-1", 5));
            Assert.Equal(5 * Activities.DefaultPrice, subtotal);

            // Add an invalid item
            var err = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => AddItemAsync(new(Activities.InvalidSku, 10)));
            Assert.Equal(
                "ItemUnavailable",
                Assert.IsType<ApplicationFailureException>(err.InnerException).ErrorType);

            // Add another and checkout
            await AddItemAsync(new("sku-2", 10));
            await handle.SignalAsync(wf => wf.CheckoutAsync());
            var result = await handle.GetResultAsync<ShoppingCartWorkflow.FinalizedOrder>();
            Assert.Equal(workflowId, result.Id);
            Assert.Equal(
                [
                    new(new("sku-1", 5), 5 * Activities.DefaultPrice),
                    new(new("sku-2", 10), 10 * Activities.DefaultPrice),
                ],
                result.Items.ToArray());
            Assert.Equal(
                (5 * Activities.DefaultPrice) + (10 * Activities.DefaultPrice),
                result.Total);
        });
    }
}