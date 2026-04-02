namespace TemporalioSamples.Testcontainers;

using Microsoft.Extensions.Logging;
using Npgsql;
using Temporalio.Activities;

public class OrderActivities(string connectionString)
{
    [Activity]
    public async Task<int> CheckInventoryAsync(string productId)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Checking inventory for {ProductId}",
            productId);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT quantity FROM inventory WHERE product_id = @productId",
            conn);
        cmd.Parameters.AddWithValue("productId", productId);

        var result = await cmd.ExecuteScalarAsync();
        return result is int qty ? qty : 0;
    }

    [Activity]
    public async Task ReserveInventoryAsync(
        string productId,
        int quantity)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Reserving {Quantity}x {ProductId}",
            quantity,
            productId);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "UPDATE inventory SET quantity = quantity - @qty WHERE product_id = @productId AND quantity >= @qty",
            conn);
        cmd.Parameters.AddWithValue("qty", quantity);
        cmd.Parameters.AddWithValue("productId", productId);

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
        {
            throw new InvalidOperationException($"Failed to reserve {quantity} units of {productId}");
        }
    }

    [Activity]
    public async Task ReleaseInventoryAsync(
        string productId,
        int quantity)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Releasing {Quantity}x {ProductId} back to inventory",
            quantity,
            productId);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "UPDATE inventory SET quantity = quantity + @qty WHERE product_id = @productId",
            conn);
        cmd.Parameters.AddWithValue("qty", quantity);
        cmd.Parameters.AddWithValue("productId", productId);
        await cmd.ExecuteNonQueryAsync();
    }

    [Activity]
    public async Task<string> ProcessPaymentAsync(
        string productId,
        int quantity,
        string paymentMethod)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Processing payment via {PaymentMethod} for {Quantity}x {ProductId}",
            paymentMethod,
            quantity,
            productId);

        // Simulate payment failure for a specific payment method.
        if (paymentMethod == "invalid-card")
        {
            throw new InvalidOperationException("Payment declined: invalid card");
        }

        var paymentId = Guid.NewGuid().ToString("N")[..12];

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "INSERT INTO payments (payment_id, product_id, quantity, payment_method) VALUES (@paymentId, @productId, @qty, @method)",
            conn);
        cmd.Parameters.AddWithValue("paymentId", paymentId);
        cmd.Parameters.AddWithValue("productId", productId);
        cmd.Parameters.AddWithValue("qty", quantity);
        cmd.Parameters.AddWithValue("method", paymentMethod);
        await cmd.ExecuteNonQueryAsync();

        return paymentId;
    }

    [Activity]
    public async Task<string> CreateOrderAsync(
        string productId,
        int quantity,
        string paymentId)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Creating order for {Quantity}x {ProductId} with payment {PaymentId}",
            quantity,
            productId,
            paymentId);

        var orderId = Guid.NewGuid().ToString("N")[..12];

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "INSERT INTO orders (order_id, product_id, quantity, payment_id, status) VALUES (@orderId, @productId, @qty, @paymentId, @status)",
            conn);
        cmd.Parameters.AddWithValue("orderId", orderId);
        cmd.Parameters.AddWithValue("productId", productId);
        cmd.Parameters.AddWithValue("qty", quantity);
        cmd.Parameters.AddWithValue("paymentId", paymentId);
        cmd.Parameters.AddWithValue("status", OrderStatus.Confirmed.ToString());
        await cmd.ExecuteNonQueryAsync();

        return orderId;
    }

    [Activity]
    public async Task SendConfirmationAsync(
        string orderId,
        string productId,
        int quantity)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Sending confirmation for order {OrderId}",
            orderId);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "INSERT INTO notifications (notification_id, order_id, message) VALUES (@notifId, @orderId, @message)",
            conn);
        cmd.Parameters.AddWithValue("notifId", Guid.NewGuid().ToString("N")[..12]);
        cmd.Parameters.AddWithValue("orderId", orderId);
        cmd.Parameters.AddWithValue("message", $"Order {orderId} confirmed: {quantity}x {productId}");
        await cmd.ExecuteNonQueryAsync();
    }
}
