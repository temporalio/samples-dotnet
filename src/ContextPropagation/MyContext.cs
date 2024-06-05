namespace TemporalioSamples.ContextPropagation;

public static class MyContext
{
    public static readonly AsyncLocal<string> UserId = new();
}