namespace TemporalioSamples.Polling.Common;

public interface IPollingActivity
{
    Task<string> DoPollAsync();
}