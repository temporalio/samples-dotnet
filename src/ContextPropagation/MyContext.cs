namespace TemporalioSamples.ContextPropagation;

public static class MyContext
{
    public static readonly AsyncLocal<string?> UserIdLocal = new();

    public static string UserId
    {
        get => UserIdLocal.Value ?? "<unknown>";
        set => UserIdLocal.Value = value;
    }
}