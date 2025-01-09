namespace TemporalioSamples.UpdateWithStartLazyInit;

using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class ShoppingCartWorkflow
{
    public record FinalizedOrder(
        string Id,
        IReadOnlyCollection<PricedItem> Items,
        decimal Total)
    {
        public override string ToString()
        {
            var items = string.Join(",", Items.Select(i =>
                $"{{ sku: {i.Item.Sku}, quantity: {i.Item.Quantity}, price: {i.Price} }}"));
            return $"order id: {Id}, items: [{items}], total: {Total}";
        }
    }

    public record PricedItem(ShoppingCartItem Item, decimal Price);

    private readonly List<PricedItem> items = new();
    private bool orderSubmitted;

    [WorkflowRun]
    public async Task<FinalizedOrder> RunAsync()
    {
        // Wait for order submission and then return finalized order
        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished && orderSubmitted);
        return new(Workflow.Info.WorkflowId, items, Total);
    }

    [WorkflowQuery]
    public decimal Total => items.Sum(item => item.Price);

    [WorkflowUpdateValidator(nameof(AddItemAsync))]
    public void ValidateAddItem(ShoppingCartItem item)
    {
        if (orderSubmitted)
        {
            throw new ApplicationFailureException("Order already submitted");
        }
    }

    [WorkflowUpdate]
    public async Task<decimal> AddItemAsync(ShoppingCartItem item)
    {
        // Get price or fail
        var maybePrice = await Workflow.ExecuteActivityAsync(
            () => Activities.GetPriceAsync(item),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        var price = maybePrice ??
            throw new ApplicationFailureException($"Item unavailable: {item}", "ItemUnavailable");

        // Add item and return new total
        items.Add(new(item, price));
        return Total;
    }

    [WorkflowSignal]
    public async Task CheckoutAsync() => orderSubmitted = true;
}