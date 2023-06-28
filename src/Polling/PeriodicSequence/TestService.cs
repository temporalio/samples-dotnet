namespace TemporalioSamples.Polling.PeriodicSequence;

public class TestService
{
    private readonly int errorAttempts;
    private int tryAttempt;

    public TestService(int errorAttempts = 5) => this.errorAttempts = errorAttempts;

    public async Task<string> GetServiceResultAsync(CancellationToken cancellationToken)
    {
        // Fake delay to simulate a service call
        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        tryAttempt++;

        if (tryAttempt % errorAttempts == 0)
        {
            return "OK";
        }

        throw new TestServiceException("Service is down");
    }
}