namespace TemporalioSamples.ContextPropagation;

// Identity must be serializable.
// We are using this to pass (via headers) the properties we'd use to
// fetch IPrincipal, DbContext, etc
public class Identity
{
    public string ClientId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Business Context {ClientId} : {UserId}";
    }
}