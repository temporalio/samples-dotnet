using System.CommandLine;
using TemporalioSamples.EnvConfig;

async Task LoadFromFileAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    await LoadFromFile.RunAsync();
}

async Task LoadProfileAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    await LoadProfile.RunAsync();
}

var loadFromFileCommand = new Command("load-from-file", "Load default profile from config.toml");
loadFromFileCommand.SetAction(LoadFromFileAsync);

var loadProfileCommand = new Command("load-profile", "Load staging profile with programmatic overrides");
loadProfileCommand.SetAction(LoadProfileAsync);

var rootCommand = new RootCommand("Environment configuration sample") { loadFromFileCommand, loadProfileCommand };
return await rootCommand.Parse(args).InvokeAsync();
