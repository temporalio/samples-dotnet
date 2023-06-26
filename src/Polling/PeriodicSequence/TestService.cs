namespace TemporalioSamples.Polling.PeriodicSequence;

public class TestService
{
    private readonly int errorAttempts;
    private int tryAttempt;

    public TestService(int errorAttempts = 5) => this.errorAttempts = errorAttempts;

    public Task<string> GetServiceResultAsync(CancellationToken cancellationToken)
    {
        tryAttempt++;

        if (tryAttempt % errorAttempts == 0)
        {
            return Task.FromResult("OK");
        }

        throw new TestServiceException("Service is down");
    }
}