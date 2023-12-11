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

        var options = new ActivityOptions()
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(90), // schedule a retry if the Activity function doesn't return within 90 seconds
            RetryPolicy = new()
            {
                InitialInterval = TimeSpan.FromSeconds(15), // first try will occur after 15 seconds
                BackoffCoefficient = 1, // double the delay after each retry
                MaximumInterval = TimeSpan.FromMinutes(1), // up to a maximum delay of 1 minute
                MaximumAttempts = 2, // fail the Activitiesivity after 2 attempts
            },
        };

        try
        {
            await Workflow.ExecuteActivityAsync(() => Activities.Withdraw(transfer), options);

            compensations.Add(async () => await Workflow.ExecuteActivityAsync(
                                  () => Activities.WithdrawCompensation(transfer),
                                  options));

            await Workflow.ExecuteActivityAsync(() => Activities.Deposit(transfer), options);

            compensations.Add(async () => await Workflow.ExecuteActivityAsync(
                       () => Activities.DepositCompensation(transfer),
                       options));

            // throw new Exception
            await Workflow.ExecuteActivityAsync(() => Activities.StepWithError(transfer), options);
        }
        catch (Exception)
        {
            logger.LogInformation("Exception caught. Initiating compensation...");
            compensations.Reverse();
            foreach (var comp in compensations)
            {
                await comp.Invoke();
            }
            throw;
        }
    }
}