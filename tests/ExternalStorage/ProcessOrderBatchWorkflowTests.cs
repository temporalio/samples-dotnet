namespace TemporalioSamples.Tests.ExternalStorage;

using System.Net;
using System.Net.Sockets;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.ExternalStorage;
using Xunit;
using Xunit.Abstractions;

public class ProcessOrderBatchWorkflowTests : WorkflowEnvironmentTestBase
{
    public ProcessOrderBatchWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_LargeOrderList_OffloadsToS3()
    {
        var port = GetFreePort();
        using var mockCancel = new CancellationTokenSource();
        var mock = S3Mock.RunAsync(mockCancel.Token, port);
        try
        {
            using var s3Client = Storage.CreateMockS3Client(port);
            var options = (TemporalClientOptions)Client.Options.Clone();
            options.DataConverter = Storage.CreateDataConverter(s3Client, Storage.MockBucket);
            var client = new TemporalClient(Client.Connection, options);

            using var worker = new TemporalWorker(
                client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddActivity(OrderActivities.FetchOrders).
                    AddActivity(OrderActivities.SummarizeOrders).
                    AddWorkflow<ProcessOrderBatchWorkflow>());
            await worker.ExecuteAsync(async () =>
            {
                var handle = await client.StartWorkflowAsync(
                    (ProcessOrderBatchWorkflow wf) => wf.RunAsync(200),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                    {
                        // If the mock is unreachable, activities retry forever, so the test would
                        // hang instead of failing.
                        RunTimeout = TimeSpan.FromMinutes(1),
                    });
                var summary = await handle.GetResultAsync();
                Assert.Equal(200, summary.OrderCount);
                Assert.Equal(5000, summary.TotalWeightKg);

                // The sample only demonstrates anything if the order list is still above the
                // threshold after compression.
                var history = await handle.FetchHistoryAsync();
                var fetchOrdersResult = history.Events.
                    First(e => e.ActivityTaskCompletedEventAttributes != null).
                    ActivityTaskCompletedEventAttributes.Result.Payloads_.Single();
                Assert.NotEmpty(fetchOrdersResult.ExternalPayloads);
            });
        }
        finally
        {
            await mockCancel.CancelAsync();
            await mock;
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
