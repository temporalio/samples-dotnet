namespace TemporalioSamples.Tests;

using System.CommandLine;
using System.CommandLine.Help;

public static class Program
{
    private static readonly Option<bool> VerboseOption = new("-verbose")
    {
        Description = "Enable trace logging and verbose xunit output",
    };

    internal static bool InProc { get; private set; }

    internal static bool Verbose { get; private set; }

    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Run sample tests in-process with the xunit console runner")
        {
            VerboseOption,
        };
        // All other options are passed through to xunit, including --help
        rootCommand.TreatUnmatchedTokensAsErrors = false;
        rootCommand.Options.Remove(rootCommand.Options.OfType<HelpOption>().Single());
        rootCommand.Options.Remove(rootCommand.Options.OfType<VersionOption>().Single());
        rootCommand.SetAction(RunTests);
        return rootCommand.Parse(args).Invoke();
    }

    private static int RunTests(ParseResult parseResult)
    {
        InProc = true;
        Verbose = parseResult.GetValue(VerboseOption);
        var xunitArgs = parseResult.UnmatchedTokens.ToList();
        if (Verbose)
        {
            xunitArgs.Add("-verbose");
        }
        // Always put self assembly as first arg if "--help" isn't the only arg
        if (xunitArgs.Count != 1 || xunitArgs[0] != "--help")
        {
            xunitArgs.Insert(0, typeof(Program).Assembly.Location);
        }
        return Xunit.ConsoleClient.Program.Main(xunitArgs.ToArray());
    }
}
