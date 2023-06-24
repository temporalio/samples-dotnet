using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using TemporalioSamples.Polling.Common;

namespace TemporalioSamples.Polling.Infrequent;

public class InfrequentPollingActivity : IPollingActivity
{
    private readonly TestService service;

    public InfrequentPollingActivity(TestService service) => this.service = service;

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
            // We want to rethrow the service exception so we can poll via activity retries
            throw;
        }
    }
}