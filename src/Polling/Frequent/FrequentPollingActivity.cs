using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using TemporalioSamples.Polling.Common;

namespace TemporalioSamples.Polling.Frequent;

public class FrequentPollingActivity : IPollingActivity
{
    private static int pollDurationSections = 1;

    private readonly TestService service;

    public FrequentPollingActivity(TestService service) => this.service = service;

    [Activity]
    public async Task<string> DoPollAsync()
    {
        while (true)
        {
            try
            {
                return await service.GetServiceResultAsync();
            }
            catch (TestServiceException)
            {
                ActivityExecutionContext.Current.Logger.LogInformation("Test service was down");
            }

            // Heart beat and sleep for the poll duration
            try
            {
                ActivityExecutionContext.Current.Heartbeat();
            }
            catch (ActivityFailureException)
            {
                // activity was either cancelled or workflow was completed or worker shut down
                throw;
            }

            await Task.Delay(TimeSpan.FromSeconds(pollDurationSections));
        }
    }
}