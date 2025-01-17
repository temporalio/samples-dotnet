using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Saga;

[Workflow]
public class SagaWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(TransferDetails transfer)
    {
        List<Func<Task>> compensations = new();
        var logger = Workflow.Logger;

        var options = new ActivityOptions() { StartToCloseTimeout = TimeSpan.FromSeconds(90) };

        try
        {
            compensations.Add(async () => await Workflow.ExecuteActivityAsync(
                        () => Activities.WithdrawCompensation(transfer),
                        options));

            await Workflow.ExecuteActivityAsync(() => Activities.Withdraw(transfer), options);

            compensations.Add(async () => await Workflow.ExecuteActivityAsync(
                       () => Activities.DepositCompensation(transfer),
                       options));

            await Workflow.ExecuteActivityAsync(() => Activities.Deposit(transfer), options);

            // throw new Exception
            await Workflow.ExecuteActivityAsync(() => Activities.StepWithError(transfer), options);
        }
        catch (Exception)
        {
            logger.LogInformation("Exception caught. Initiating compensation...");
            await CompensateAsync(compensations);
            throw;
        }
    }

    private async Task CompensateAsync(List<Func<Task>> compensations)
    {
        compensations.Reverse();
        foreach (var comp in compensations)
        {
#pragma warning disable CA1031
            try
            {
                await comp.Invoke();
            }
            catch (Exception ex)
            {
                Workflow.Logger.LogError(ex, "Failed to compensate");
                // swallow errors
            }
#pragma warning restore CA1031
        }
    }
}