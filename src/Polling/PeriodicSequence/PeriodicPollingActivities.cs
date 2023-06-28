namespace TemporalioSamples.Polling.PeriodicSequence;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class PeriodicPollingActivities
{
    private readonly TestService service;

    public PeriodicPollingActivities(TestService service) => this.service = service;

    [Activity]
    public async Task<string> DoPollAsync()
    {
        try
        {
            return await service.GetServiceResultAsync(ActivityExecutionContext.Current.CancellationToken);
        }
        catch (TestServiceException)
        {
            ActivityExecutionContext.Current.Logger.LogInformation("Test service was down");
            throw;
        }
    }
}