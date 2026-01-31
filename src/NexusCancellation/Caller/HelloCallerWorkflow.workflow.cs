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

        var tasks = Languages.Select(lang =>
            client.ExecuteNexusOperationAsync(
                svc => svc.SayHello(new IHelloService.HelloInput(name, lang)),
                new NexusOperationOptions
                {
                    CancellationType = NexusOperationCancellationType.WaitCancellationRequested,
                    CancellationToken = cts.Token,
                })).ToList();

        var firstTask = await Workflow.WhenAnyAsync(tasks);

        Workflow.Logger.LogInformation("First operation completed, cancelling remaining operations");

#pragma warning disable CA1849, VSTHRD103 // CancelAsync() is non-deterministic in workflows
        cts.Cancel(); // Cancel the rest of the operations
#pragma warning restore CA1849, VSTHRD103

        try
        {
            // Wait for all other operations to complete.
            await Workflow.WhenAllAsync(tasks);
        }
        catch (Exception ex) when (TemporalException.IsCanceledException(ex))
        {
            Workflow.Logger.LogInformation("Operation was cancelled");
        }

        var result = await firstTask;
        return result?.Message ?? throw new ApplicationFailureException("No successful result");
    }
}