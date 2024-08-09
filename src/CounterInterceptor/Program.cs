namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Client;
using Temporalio.Worker;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var clientInterceptor = new SimpleClientCallsInterceptor();
        var client = await TemporalClient.ConnectAsync(
            options: new("localhost:7233")
            {
                Interceptors = new[]
                {
                    clientInterceptor,
                },
            });

        using var tokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            tokenSource.Cancel();
            eventArgs.Cancel = true;
        };

        var activities = new MyActivities();

        var taskQueue = "CounterInterceptorTaskQueue";

        var workerOptions = new TemporalWorkerOptions(taskQueue).
                AddAllActivities(activities).
                AddWorkflow<MyWorkflow>().
                AddWorkflow<MyChildWorkflow>();

        var workerInterceptor = new SimpleCounterWorkerInterceptor();
        workerOptions.Interceptors = new[] { workerInterceptor };

        using var worker = new TemporalWorker(
            client,
            workerOptions);

        // Run worker until cancelled
        Console.WriteLine("Running worker...");
        try
        {
            // Start the workers
            var workerResult = worker.ExecuteAsync(tokenSource.Token);

            // Start the workflow
            var handle = await client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(),
                new(id: Guid.NewGuid().ToString(), taskQueue: taskQueue));

            Console.WriteLine("Sending name and title to workflow");
            await handle.SignalAsync(wf => wf.SignalNameAndTitleAsync("John", "Customer"));

            var name = await handle.QueryAsync(wf => wf.Name);
            var title = await handle.QueryAsync(wf => wf.Title);

            // Send exit signal to workflow
            await handle.SignalAsync(wf => wf.ExitAsync());

            var result = await handle.GetResultAsync();

            Console.WriteLine($"Workflow result is {result}", result);

            Console.WriteLine("Query results: ");
            Console.WriteLine($"\tName: {name}", name);
            Console.WriteLine($"\tTitle: {title}", title);

            // Print worker counter info
            Console.WriteLine("Collected Worker Counter Info: ");
            Console.WriteLine(workerInterceptor.Info());

            // Print client counter info
            Console.WriteLine();
            Console.WriteLine("Collected Client Counter Info:");
            Console.WriteLine(clientInterceptor.Info());
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Worker cancelled");
        }
    }
}