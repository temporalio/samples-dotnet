namespace TemporalioSamples.ContextPropagation;

public static class IdentityContext
{
    public static readonly AsyncLocal<Identity> User = new();
}