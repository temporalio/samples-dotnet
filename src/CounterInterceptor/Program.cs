namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var counterInterceptor = new MyCounterInterceptor();
        var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
        connectOptions.TargetHost ??= "localhost:7233";
        connectOptions.Interceptors = new[]
        {
            counterInterceptor,
        };
        var client = await TemporalClient.ConnectAsync(connectOptions);

        var activities = new MyActivities();

        var taskQueue = "CounterInterceptorTaskQueue";

        var workerOptions = new TemporalWorkerOptions(taskQueue).
                AddAllActivities(activities).
                AddWorkflow<MyWorkflow>().
                AddWorkflow<MyChildWorkflow>();

        // workerOptions.Interceptors = new[] { counterInterceptor };
        using var worker = new TemporalWorker(
            client,
            workerOptions);

        // Run worker until cancelled
        Console.WriteLine("Running worker...");

        // Start the workers
        await worker.ExecuteAsync(async () =>
        {
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

            Console.WriteLine($"Workflow result is {result}");

            Console.WriteLine("Query results: ");
            Console.WriteLine($"\tName: {name}");
            Console.WriteLine($"\tTitle: {title}");

            // Print worker counter info
            Console.WriteLine("\nCollected Worker Counter Info:\n");
            Console.WriteLine(counterInterceptor.WorkerInfo());
            Console.WriteLine($"Number of unique workflows: {counterInterceptor.Counts.Count}");

            // Print client counter info
            Console.WriteLine();
            Console.WriteLine("Collected Client Counter Info:\n");
            Console.WriteLine(counterInterceptor.ClientInfo());
        });
    }
}