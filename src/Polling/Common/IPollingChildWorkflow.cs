namespace TemporalioSamples.Polling.Common;

public record PollingChildWorkflowArgs(int PollingIntervalInSeconds);

public interface IPollingChildWorkflow
{
    Task<string> RunAsync(PollingChildWorkflowArgs args);
}