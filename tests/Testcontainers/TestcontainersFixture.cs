namespace TemporalioSamples.Tests.Testcontainers;

using global::Testcontainers.PostgreSql;
using global::Testcontainers.Temporal;
using Npgsql;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.Testcontainers;
using Xunit;

#pragma warning disable CA1001 // Disposable fields are cleaned up in IAsyncLifetime.DisposeAsync
public class TestcontainersFixture : IAsyncLifetime
#pragma warning restore CA1001
{
    public const string TaskQueue = "order-processing";

    private readonly TemporalContainer temporalContainer =
        new TemporalBuilder("temporalio/temporal:latest").Build();

    private readonly PostgreSqlContainer postgresContainer =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private readonly CancellationTokenSource workerCts = new();

    private TemporalWorker? worker;
    private Task? workerTask;

    public TemporalClient Client { get; private set; } = null!;

    public string PostgresConnectionString => postgresContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        // Start both containers in parallel.
        await Task.WhenAll(
            temporalContainer.StartAsync(),
            postgresContainer.StartAsync());

        // Create tables.
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            CREATE TABLE inventory (
                product_id TEXT PRIMARY KEY,
                quantity INT NOT NULL DEFAULT 0
            );
            CREATE TABLE payments (
                payment_id TEXT PRIMARY KEY,
                product_id TEXT NOT NULL,
                quantity INT NOT NULL,
                payment_method TEXT NOT NULL
            );
            CREATE TABLE orders (
                order_id TEXT PRIMARY KEY,
                product_id TEXT NOT NULL,
                quantity INT NOT NULL,
                payment_id TEXT NOT NULL,
                status TEXT NOT NULL
            );
            CREATE TABLE notifications (
                notification_id TEXT PRIMARY KEY,
                order_id TEXT NOT NULL,
                message TEXT NOT NULL
            );
            """,
            conn);
        await cmd.ExecuteNonQueryAsync();

        // Connect client.
        Client = await TemporalClient.ConnectAsync(new(temporalContainer.GetGrpcAddress()));

        // Start worker (runs for the lifetime of the fixture).
        var activities = new OrderActivities(PostgresConnectionString);
        worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(TaskQueue)
                .AddActivity(activities.CheckInventoryAsync)
                .AddActivity(activities.ReserveInventoryAsync)
                .AddActivity(activities.ReleaseInventoryAsync)
                .AddActivity(activities.ProcessPaymentAsync)
                .AddActivity(activities.CreateOrderAsync)
                .AddActivity(activities.SendConfirmationAsync)
                .AddWorkflow<OrderWorkflow>());
        workerTask = worker.ExecuteAsync(workerCts.Token);
    }

    public async Task DisposeAsync()
    {
        await workerCts.CancelAsync();
        try
        {
            if (workerTask != null)
            {
                await workerTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        worker?.Dispose();
        workerCts.Dispose();

        await temporalContainer.DisposeAsync();
        await postgresContainer.DisposeAsync();
    }

    public async Task SeedInventoryAsync(string productId, int quantity)
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO inventory (product_id, quantity) VALUES (@productId, @qty)
            ON CONFLICT (product_id) DO UPDATE SET quantity = @qty
            """,
            conn);
        cmd.Parameters.AddWithValue("productId", productId);
        cmd.Parameters.AddWithValue("qty", quantity);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetInventoryAsync(string productId)
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT quantity FROM inventory WHERE product_id = @productId",
            conn);
        cmd.Parameters.AddWithValue("productId", productId);

        var result = await cmd.ExecuteScalarAsync();
        return result is int qty ? qty : 0;
    }

    public async Task<(string ProductId, int Quantity, string PaymentId, string Status)?> GetOrderFromDbAsync(
        string orderId)
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT product_id, quantity, payment_id, status FROM orders WHERE order_id = @orderId",
            conn);
        cmd.Parameters.AddWithValue("orderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3));
    }

    public async Task<bool> PaymentExistsAsync(string paymentId)
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM payments WHERE payment_id = @paymentId",
            conn);
        cmd.Parameters.AddWithValue("paymentId", paymentId);

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task<string?> GetNotificationAsync(string orderId)
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT message FROM notifications WHERE order_id = @orderId",
            conn);
        cmd.Parameters.AddWithValue("orderId", orderId);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }
}
