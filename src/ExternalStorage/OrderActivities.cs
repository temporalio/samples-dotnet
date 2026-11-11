namespace TemporalioSamples.ExternalStorage;

using Temporalio.Activities;

public record Order(string Id, double WeightKg, string Notes);

public record BatchSummary(int OrderCount, double TotalWeightKg);

public static class OrderActivities
{
    // Random notes don't compress well, so the order list stays above the external storage
    // threshold after the codec compresses it.
    private const int NotesGuidsPerOrder = 100;

    [Activity]
    public static IReadOnlyList<Order> FetchOrders(int orderCount) =>
        Enumerable.Range(1, orderCount).Select(i => new Order(
            $"ORD-{i:D6}",
            (i % 50) + 0.5,
            string.Concat(Enumerable.Range(0, NotesGuidsPerOrder).Select(_ => Guid.NewGuid())))).ToList();

    [Activity]
    public static BatchSummary SummarizeOrders(IReadOnlyList<Order> orders) =>
        new(orders.Count, orders.Sum(o => o.WeightKg));
}
