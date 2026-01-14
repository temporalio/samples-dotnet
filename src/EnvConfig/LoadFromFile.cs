using Temporalio.Client;
using Temporalio.Common.EnvConfig;

namespace TemporalioSamples.EnvConfig;

/// <summary>
/// Sample demonstrating loading the default environment configuration profile
/// from a TOML file.
/// </summary>
public static class LoadFromFile
{
    public static async Task RunAsync()
    {
        Console.WriteLine("--- Loading default profile from config.toml ---");

        try
        {
            // For this sample to be self-contained, we explicitly provide the path to
            // the config.toml file included in this directory.
            // By default though, the config.toml file will be loaded from
            // ~/.config/temporalio/temporal.toml (or the equivalent standard config directory on your OS).
            var configFile = Path.Combine(Directory.GetCurrentDirectory(), "config.toml");

            // LoadClientConnectOptions is a helper that loads a profile and prepares
            // the config for TemporalClient.ConnectAsync. By default, it loads the
            // "default" profile.
            var connectOptions = ClientEnvConfig.LoadClientConnectOptions(new ClientEnvConfig.ProfileLoadOptions
            {
                ConfigSource = DataSource.FromPath(configFile),
            });

            Console.WriteLine($"Loaded 'default' profile from {configFile}.");
            Console.WriteLine($"  Address: {connectOptions.TargetHost}");
            Console.WriteLine($"  Namespace: {connectOptions.Namespace}");
            if (connectOptions.RpcMetadata?.Count > 0)
            {
                Console.WriteLine($"  gRPC Metadata: {string.Join(", ", connectOptions.RpcMetadata.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }

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