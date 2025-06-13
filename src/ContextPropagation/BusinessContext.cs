using System.Security.Principal;

namespace TemporalioSamples.ContextPropagation;

public static class BusinessContext
{
    public static readonly AsyncLocal<IPrincipal> CurrentPrincipal = new();
}