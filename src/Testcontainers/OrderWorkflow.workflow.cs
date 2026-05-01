namespace TemporalioSamples.Testcontainers;

using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class OrderWorkflow
{
    private static readonly ActivityOptions DefaultActivityOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
    };

    private OrderStatus status = OrderStatus.Pending;
    private string? orderId;

    [WorkflowQuery]
    public OrderStatus CurrentStatus => status;

    [WorkflowQuery]
    public string? OrderId => orderId;

    [WorkflowRun]
    public async Task<OrderResult> RunAsync(OrderRequest request)
    {
        Workflow.Logger.LogInformation(
            "Starting order pipeline for {Quantity}x {ProductId}",
            request.Quantity,
            request.ProductId);

        // Step 1: Check inventory availability.
        var available = await Workflow.ExecuteActivityAsync(
            (OrderActivities act) => act.CheckInventoryAsync(request.ProductId),
            DefaultActivityOptions);

        if (available < request.Quantity)
        {
            status = OrderStatus.Failed;
            return new OrderResult(
                string.Empty,
                OrderStatus.Failed,
                $"Insufficient inventory: requested {request.Quantity}, available {available}");
        }

        // Step 2: Reserve inventory (deduct stock).
        await Workflow.ExecuteActivityAsync(
            (OrderActivities act) => act.ReserveInventoryAsync(request.ProductId, request.Quantity),
            DefaultActivityOptions);
        status = OrderStatus.InventoryHeld;

        // Step 3: Process payment. If this fails, compensate by releasing inventory.
        string paymentId;
        try
        {
            paymentId = await Workflow.ExecuteActivityAsync(
                (OrderActivities act) => act.ProcessPaymentAsync(
                    request.ProductId,
                    request.Quantity,
                    request.PaymentMethod),
                new ActivityOptions
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new() { MaximumAttempts = 1 },
                });
        }
        catch (ActivityFailureException ex)
        {
            Workflow.Logger.LogWarning("Payment failed, compensating: {Message}", ex.Message);

            // Compensation: release the reserved inventory.
            await Workflow.ExecuteActivityAsync(
                (OrderActivities act) => act.ReleaseInventoryAsync(request.ProductId, request.Quantity),
                DefaultActivityOptions);

            status = OrderStatus.Failed;
            return new OrderResult(string.Empty, OrderStatus.Failed, "Payment failed");
        }

        status = OrderStatus.PaymentProcessed;

        // Step 4: Create the order record.
        orderId = await Workflow.ExecuteActivityAsync(
            (OrderActivities act) => act.CreateOrderAsync(
                request.ProductId,
                request.Quantity,
                paymentId),
            DefaultActivityOptions);

        // Step 5: Send confirmation notification.
        await Workflow.ExecuteActivityAsync(
            (OrderActivities act) => act.SendConfirmationAsync(
                orderId,
                request.ProductId,
                request.Quantity),
            DefaultActivityOptions);

        status = OrderStatus.Confirmed;
        Workflow.Logger.LogInformation("Order {OrderId} fully confirmed", orderId);

        return new OrderResult(orderId, OrderStatus.Confirmed);
    }
}
