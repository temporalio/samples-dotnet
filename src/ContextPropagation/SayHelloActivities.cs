namespace TemporalioSamples.ContextPropagation;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class SayHelloActivities
{
    [Activity]
    public string SayHello(string name)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Activity called by user {User}",
            IdentityContext.User.ToString());

        var principaledName = BusinessContext.CurrentPrincipal.Value?.Identity?.Name;
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Activity called with principal {Principal}",
            BusinessContext.CurrentPrincipal.Value?.Identity?.Name);

        return $"Hello, {name} {principaledName}!";
    }
}