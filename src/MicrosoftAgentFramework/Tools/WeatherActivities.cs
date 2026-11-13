// @@@SNIPSTART dotnet-microsoft-agent-framework-tools-activity
namespace TemporalioSamples.MicrosoftAgentFramework.Tools;

using System.ComponentModel;
using Temporalio.Activities;

public record WeatherReport(string City, int TemperatureF, string Conditions);

public static class WeatherActivities
{
    // Stands in for an HTTP call. Anything that does I/O belongs in an Activity. The model sees
    // the schema built from the signature and the Description attributes.
    [Activity]
    [Description("Looks up the current weather for a city")]
    public static Task<WeatherReport> GetWeatherAsync([Description("City name")] string city) =>
        Task.FromResult(new WeatherReport(city, 72, "sunny"));
}
// @@@SNIPEND
