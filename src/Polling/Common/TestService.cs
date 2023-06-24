namespace TemporalioSamples.Polling.Common;

public class TestService
{
    private int tryAttempt;
    private int errorAttempts = 5;

    public TestService()
    {
    }

    public TestService(int errorAttempts) => this.errorAttempts = errorAttempts;

    public Task<string> GetServiceResultAsync()
    {
        tryAttempt++;

        if (tryAttempt % errorAttempts == 0)
        {
            return Task.FromResult("OK");
        }

        throw new TestServiceException("Service is down");
    }
}