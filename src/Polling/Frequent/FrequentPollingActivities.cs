namespace TemporalioSamples.Polling.Frequent;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class FrequentPollingActivities
{
    private readonly TestService service;

    public FrequentPollingActivities(TestService service) => this.service = service;

    [Activity]
    public async Task<string> DoPollAsync()
    {
        while (true)
        {
            try
            {
                return await service.GetServiceResultAsync(ActivityExecutionContext.Current.CancellationToken);
            }
            catch (TestServiceException)
            {
                ActivityExecutionContext.Current.Logger.LogInformation("Test service was down");
            }

            // Heart beat and sleep for the poll duration
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(TimeSpan.FromSeconds(1), ActivityExecutionContext.Current.CancellationToken);
        }
    }
}