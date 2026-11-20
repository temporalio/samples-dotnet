// @@@SNIPSTART dotnet-microsoft-agent-framework-tools-tool
namespace TemporalioSamples.MicrosoftAgentFramework.Tools;

using System.ComponentModel;

// Pure logic with no I/O runs in the Workflow as a plain AIFunction and leaves no Activity.
public static class LetterTools
{
    [Description("Counts how many times a letter appears in a word, ignoring case")]
    public static int CountLetters(
        [Description("The word to search")] string word,
        [Description("A single letter")] string letter) =>
        string.IsNullOrEmpty(letter)
            ? 0
            : word.Count(c => char.ToUpperInvariant(c) == char.ToUpperInvariant(letter[0]));
}
// @@@SNIPEND
