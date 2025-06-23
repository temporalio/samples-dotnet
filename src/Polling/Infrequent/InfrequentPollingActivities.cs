namespace TemporalioSamples.Polling.Infrequent;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Exceptions;

public class InfrequentPollingActivities
{
    private readonly TestService service;

    public InfrequentPollingActivities(TestService service) => this.service = service;

    [Activity]
    public async Task<string> DoPollAsync()
    {
        try
        {
            return await service.GetServiceResultAsync(ActivityExecutionContext.Current.CancellationToken);
        }
        catch (TestServiceException e)
        {
            ActivityExecutionContext.Current.Logger.LogInformation("Test service was down");
            // We want to rethrow the service exception so we can poll via activity retries
            throw new ApplicationFailureException("Service is down", inner: e, category: ApplicationErrorCategory.Benign);
        }
    }
}