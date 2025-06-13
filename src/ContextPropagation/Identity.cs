namespace TemporalioSamples.ContextPropagation;

public class Identity
{
    public string ClientId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Business Context {ClientId} : {UserId}";
    }
}