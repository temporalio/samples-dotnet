namespace TemporalioSamples.NexusCancellation.Caller;

using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class HelloCallerWorkflow
{
    private static readonly IHelloService.HelloLanguage[] Languages =
        [IHelloService.HelloLanguage.En, IHelloService.HelloLanguage.Fr, IHelloService.HelloLanguage.De,
        IHelloService.HelloLanguage.Es, IHelloService.HelloLanguage.Tr];

    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);
        var client = Workflow.CreateNexusClient<IHelloService>(IHelloService.EndpointName);

        // Concurrently execute an operation per language.
        var tasks = Languages.Select(lang =>
            client.ExecuteNexusOperationAsync(
                svc => svc.SayHello(new IHelloService.HelloInput(name, lang)),
                new NexusOperationOptions
                {
                    // We set the CancellationType to WaitCancellationRequested, which means the caller waits
                    // for the request to be received by the handler before proceeding with the cancellation.
                    //
                    // The default CancellationType is WaitCancellationCompleted, where the caller would wait
                    // until the operation is completed.
                    CancellationType = NexusOperationCancellationType.WaitCancellationRequested,
                    CancellationToken = cts.Token,
                })).ToList();

        var firstTask = await Workflow.WhenAnyAsync(tasks);

        Workflow.Logger.LogInformation("First operation completed, cancelling remaining operations");

        // Now that the first operation has won the race, we are going to cancel the other operations.
#pragma warning disable CA1849, VSTHRD103 // CancelAsync() is non-deterministic in workflows.
        cts.Cancel();
#pragma warning restore CA1849, VSTHRD103

        // Wait for all tasks to resolve. Once the workflow completes, the server will stop trying to cancel any of
        // the operations that have not yet received cancellation, letting them run to completion. We are using the
        // CancellationType of WaitCancellationRequested so these tasks will return as soon as the operation has received
        // the cancellation request.
        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            // Only throw an error if an operation errored out not due to cancellation.
            catch (Exception ex) when (TemporalException.IsCanceledException(ex))
            {
                Workflow.Logger.LogInformation("Operation was cancelled");
            }
        }

        var result = await firstTask;
        return result?.Message ?? throw new ApplicationFailureException("No successful result");
    }
}