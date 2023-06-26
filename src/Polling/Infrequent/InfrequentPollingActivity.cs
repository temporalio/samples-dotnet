namespace TemporalioSamples.Polling.Infrequent;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class InfrequentPollingActivity
{
    private readonly TestService service;

    public InfrequentPollingActivity(TestService service) => this.service = service;

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
            // We want to rethrow the service exception so we can poll via activity retries
            throw;
        }
    }
}