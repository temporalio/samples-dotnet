namespace TemporalioSamples.Polling.Common;

public interface IPollingWorkflow
{
    Task<string> RunAsync();
}