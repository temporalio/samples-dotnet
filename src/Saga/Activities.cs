using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace TemporalioSamples.Saga;

public record TransferDetails(decimal Amount, string FromAmount, string ToAmount, string ReferenceId);

public static class Activities
{
    [Activity]
    public static void Withdraw(TransferDetails d)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Withdrawing {Amount} from account {FromAmount}. ReferenceId: {ReferenceId}", d.Amount, d.FromAmount, d.ReferenceId);
    }

    [Activity]
    public static void WithdrawCompensation(TransferDetails d)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Withdrawing Compensation {Amount} from account {FromAmount}. ReferenceId: {ReferenceId}", d.Amount, d.FromAmount, d.ReferenceId);
    }

    [Activity]
    public static void Deposit(TransferDetails d)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Depositing {Amount} into account {ToAmount}. ReferenceId: {ReferenceId}", d.Amount, d.ToAmount, d.ReferenceId);
    }

    [Activity]
    public static void DepositCompensation(TransferDetails d)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Depositing Compensation {Amount} int account {ToAmount}. ReferenceId: {ReferenceId}", d.Amount, d.ToAmount, d.ReferenceId);
    }

    [Activity]
    public static void StepWithError(TransferDetails d)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Simulate failure to trigger compensation. ReferenceId: {ReferenceId}", d.ReferenceId);
        throw new ApplicationFailureException("Simulated failure", nonRetryable: true);
    }
}