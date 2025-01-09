using Temporalio.Activities;

namespace TemporalioSamples.UpdateWithStartLazyInit;

public static class Activities
{
    public const string InvalidSku = "sku-456";
    public const decimal DefaultPrice = 5.99m;

    [Activity]
    public static async Task<decimal?> GetPriceAsync(ShoppingCartItem item)
    {
        // Simulate some time taken
        await Task.Delay(100);

        // Simulate a not-found price
        if (item.Sku == InvalidSku)
        {
            return null;
        }
        // Simulate a price
        return DefaultPrice * item.Quantity;
    }
}