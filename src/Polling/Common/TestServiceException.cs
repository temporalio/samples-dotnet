namespace TemporalioSamples.Polling.Common;

public class TestServiceException : Exception
{
    public TestServiceException()
    {
    }

    public TestServiceException(string message)
        : base(message)
    {
    }

    public TestServiceException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}