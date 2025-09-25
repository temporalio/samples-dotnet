using Temporalio.Client;
using Temporalio.Client.EnvConfig;

namespace TemporalioSamples.EnvConfig;

/// <summary>
/// Sample demonstrating loading a named environment configuration profile and
/// programmatically overriding its values.
/// </summary>
public static class LoadProfile
{
    public static async Task RunAsync()
    {
        Console.WriteLine("--- Loading 'staging' profile with programmatic overrides ---");

        try
        {
            var configFile = Path.Combine(Directory.GetCurrentDirectory(), "config.toml");
            var profileName = "staging";

            Console.WriteLine("The 'staging' profile in config.toml has an incorrect address (localhost:9999).");
            Console.WriteLine("We'll programmatically override it to the correct address.");

            // Load the 'staging' profile
            var connectOptions = ClientEnvConfig.LoadClientConnectOptions(new ClientEnvConfig.ProfileLoadOptions
            {
                Profile = profileName,
                ConfigSource = DataSource.FromPath(configFile),
            });

            // Override the target host to the correct address.
            // This is the recommended way to override configuration values.
            connectOptions.TargetHost = "localhost:7233";

            Console.WriteLine($"\nLoaded '{profileName}' profile from {configFile} with overrides.");
            Console.WriteLine($"  Address: {connectOptions.TargetHost} (overridden from localhost:9999)");
            Console.WriteLine($"  Namespace: {connectOptions.Namespace}");

            Console.WriteLine("\nAttempting to connect to client...");

            var client = await TemporalClient.ConnectAsync(connectOptions);
            Console.WriteLine("✅ Client connected successfully!");

            // Test the connection by checking the service
            var sysInfo = await client.Connection.WorkflowService.GetSystemInfoAsync(new());
            Console.WriteLine("✅ Successfully verified connection to Temporal server!\n{0}", sysInfo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"❌ Failed to connect: {ex.Message}");
        }
    }
}