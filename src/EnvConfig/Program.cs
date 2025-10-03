using TemporalioSamples.EnvConfig;

switch (args.ElementAtOrDefault(0))
{
    case "load-from-file":
        await LoadFromFile.RunAsync();
        break;
    case "load-profile":
        await LoadProfile.RunAsync();
        break;
    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run load-from-file   # Load default profile from config.toml");
        Console.WriteLine("  dotnet run load-profile     # Load staging profile with programmatic overrides");
        break;
}