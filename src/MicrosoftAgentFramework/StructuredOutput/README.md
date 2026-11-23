# Structured output

The agent fills a record. `agent.RunAsync<PersonInfo>(text)` sends the record's JSON schema to the model Activity as the response format, and the Workflow reads the parsed `PersonInfo` from `response.Result`. The Workflow returns the record itself; Temporal's default data converter serializes it, so the starter gets a typed value with no extra setup.

## What this sample demonstrates

- `RunAsync<T>` and `AgentResponse<T>.Result`
- `[Description]` attributes on the record's properties, which the schema carries to the model
- A Workflow whose return type is the record

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
```

The starter prints `Name=John Smith Age=30 Occupation=software engineer`. The Workflow `microsoft-agent-framework-structured-output` shows one `MicrosoftAgentFramework.InvokeModel` Activity.

## Files

| File | Description |
|------|-------------|
| `StructuredOutputWorkflow.workflow.cs` | `PersonInfo` and `StructuredOutputWorkflow` |
| `Program.cs` | Registers the plugin, then runs the worker or the starter, which prints the typed result |
