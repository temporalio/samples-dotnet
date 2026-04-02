namespace TemporalioSamples.Testcontainers;

public enum OrderStatus
{
    Pending,
    InventoryHeld,
    PaymentProcessed,
    Confirmed,
    Failed,
}

public record OrderRequest(string ProductId, int Quantity, string PaymentMethod);

public record OrderResult(string OrderId, OrderStatus Status, string? FailureReason = null);
