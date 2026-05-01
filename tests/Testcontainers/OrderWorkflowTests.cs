namespace TemporalioSamples.Tests.Testcontainers;

using Temporalio.Client;
using TemporalioSamples.Testcontainers;
using Xunit;
using Xunit.Abstractions;

public class OrderWorkflowTests(ITestOutputHelper output, TestcontainersFixture fixture)
    : TestBase(output), IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task HappyPath_AllStepsComplete_OrderAndPaymentAndNotificationInDatabase()
    {
        // Arrange
        var productId = $"product-{Guid.NewGuid():N}";
        await fixture.SeedInventoryAsync(productId, 100);

        // Act
        var result = await fixture.Client.ExecuteWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderRequest(productId, 5, "credit-card")),
            new(id: $"order-{Guid.NewGuid()}", taskQueue: TestcontainersFixture.TaskQueue));

        // Assert: workflow result
        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.NotEmpty(result.OrderId);

        // Assert: order row in PostgreSQL
        var dbOrder = await fixture.GetOrderFromDbAsync(result.OrderId);
        Assert.NotNull(dbOrder);
        Assert.Equal(productId, dbOrder.Value.ProductId);
        Assert.Equal(5, dbOrder.Value.Quantity);
        Assert.Equal("Confirmed", dbOrder.Value.Status);

        // Assert: payment was recorded
        Assert.True(await fixture.PaymentExistsAsync(dbOrder.Value.PaymentId));

        // Assert: inventory was deducted
        Assert.Equal(95, await fixture.GetInventoryAsync(productId));

        // Assert: confirmation notification was sent
        var notification = await fixture.GetNotificationAsync(result.OrderId);
        Assert.NotNull(notification);
        Assert.Contains(result.OrderId, notification);
    }

    [Fact]
    public async Task InsufficientInventory_FailsBeforeAnyDbWrites()
    {
        // Arrange
        var productId = $"product-{Guid.NewGuid():N}";
        await fixture.SeedInventoryAsync(productId, 2);

        // Act
        var result = await fixture.Client.ExecuteWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderRequest(productId, 10, "credit-card")),
            new(id: $"order-{Guid.NewGuid()}", taskQueue: TestcontainersFixture.TaskQueue));

        // Assert: rejected at the first step
        Assert.Equal(OrderStatus.Failed, result.Status);
        Assert.Contains("Insufficient inventory", result.FailureReason);

        // Assert: inventory was never touched
        Assert.Equal(2, await fixture.GetInventoryAsync(productId));
    }

    [Fact]
    public async Task PaymentFails_CompensatesInventory()
    {
        // Arrange
        var productId = $"product-{Guid.NewGuid():N}";
        await fixture.SeedInventoryAsync(productId, 50);

        // Act: use "invalid-card" to trigger payment failure
        var result = await fixture.Client.ExecuteWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderRequest(productId, 10, "invalid-card")),
            new(id: $"order-{Guid.NewGuid()}", taskQueue: TestcontainersFixture.TaskQueue));

        // Assert: workflow reports failure
        Assert.Equal(OrderStatus.Failed, result.Status);
        Assert.Equal("Payment failed", result.FailureReason);

        // Assert: inventory was reserved then released back (compensation)
        Assert.Equal(50, await fixture.GetInventoryAsync(productId));
    }

    [Fact]
    public async Task WorkflowQuery_ReturnsConfirmedStatus()
    {
        // Arrange
        var productId = $"product-{Guid.NewGuid():N}";
        await fixture.SeedInventoryAsync(productId, 50);

        // Act
        var handle = await fixture.Client.StartWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderRequest(productId, 3, "credit-card")),
            new(id: $"order-{Guid.NewGuid()}", taskQueue: TestcontainersFixture.TaskQueue));
        var result = await handle.GetResultAsync();

        // Assert: query the completed workflow for its final state
        Assert.Equal(OrderStatus.Confirmed, await handle.QueryAsync(wf => wf.CurrentStatus));
        Assert.Equal(result.OrderId, await handle.QueryAsync(wf => wf.OrderId));
    }

    [Fact]
    public async Task MultipleOrders_InventoryDeductsUntilDepleted()
    {
        // Arrange
        var productId = $"product-{Guid.NewGuid():N}";
        await fixture.SeedInventoryAsync(productId, 20);

        // Act: place three orders, each for 5 units
        for (var i = 0; i < 3; i++)
        {
            var confirmed = await fixture.Client.ExecuteWorkflowAsync(
                (OrderWorkflow wf) => wf.RunAsync(new OrderRequest(productId, 5, "credit-card")),
                new(id: $"order-{Guid.NewGuid()}", taskQueue: TestcontainersFixture.TaskQueue));
            Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
        }

        // Assert: 20 - (3 * 5) = 5 remaining
        Assert.Equal(5, await fixture.GetInventoryAsync(productId));

        // Act: fourth order exceeds remaining stock
        var rejected = await fixture.Client.ExecuteWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(new OrderRequest(productId, 10, "credit-card")),
            new(id: $"order-{Guid.NewGuid()}", taskQueue: TestcontainersFixture.TaskQueue));

        // Assert: rejected, inventory unchanged
        Assert.Equal(OrderStatus.Failed, rejected.Status);
        Assert.Equal(5, await fixture.GetInventoryAsync(productId));
    }
}
