using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using TemporalioSamples.Polling.Common;

namespace TemporalioSamples.Polling.PeriodicSequence;

public class PeriodicPollingActivity : IPollingActivity
{
    private readonly TestService service;

    public PeriodicPollingActivity(TestService service) => this.service = service;

    [Activity]
    public async Task<string> DoPollAsync()
    {
        try
        {
            return await service.GetServiceResultAsync();
        }
        catch (TestServiceException)
        {
            ActivityExecutionContext.Current.Logger.LogInformation("Test service was down");
            // We want to throw the service exception so we can poll via activity retries
            throw;
        }
    }
}