# Tools

Two tool patterns on one agent:

| Pattern | When to use it |
|---------|----------------|
| `AIFunctionFactory.Create` over a plain method | Pure logic with no I/O. Runs in the Workflow and leaves no Activity. |
| `ActivityAsTool.Create` over a `[Activity]` method | Anything with I/O or a clock. Runs as an Activity with its own timeout and retries. |

The prompt exercises both. The history shows one `MicrosoftAgentFramework.InvokeModel` Activity per model turn plus one `GetWeather` Activity; `CountLetters` runs in the Workflow and does not appear.

## What this sample demonstrates

- `ActivityAsTool.Create(() => WeatherActivities.GetWeatherAsync(default!), new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) })`; the plugin reads the tool's schema from the Activity's signature and `[Description]` attributes, and the argument values in the expression are placeholders
- The plugin cannot tell a pure tool from an I/O tool, so you choose per tool
- Registering the Activity on the worker next to the Workflow with `AddActivity(WeatherActivities.GetWeatherAsync)`
- A plain tool runs on the Workflow's scheduler, so keep it quick and deterministic

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
```

The starter prints the agent's answer with the letter count and the weather. The Workflow `microsoft-agent-framework-tools` shows two or three model Activities, depending on whether the model requests both tools in one turn, and one `GetWeather` Activity with the summary `microsoft-agent-framework tool call: GetWeather`.

## Files

| File | Description |
|------|-------------|
| `LetterTools.cs` | `CountLetters`, the in-Workflow tool |
| `WeatherActivities.cs` | `GetWeatherAsync`, the Activity behind the second tool |
| `ToolsWorkflow.workflow.cs` | `ToolsWorkflow` with both tools on one agent |
| `Program.cs` | Registers the plugin and the `GetWeather` Activity, then runs the worker or the starter |
