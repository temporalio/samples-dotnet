# Hello world

The smallest Agent Framework + Temporal sample: one `TemporalAgent`, invoked once. The model call runs as the `MicrosoftAgentFramework.InvokeModel` Activity with a 60-second start-to-close timeout and the plugin's default 10-second heartbeat timeout, so Temporal retries it and it survives a worker restart.

## What this sample demonstrates

- `MicrosoftAgentFrameworkPlugin` on `TemporalClientConnectOptions.Plugins`, with the OpenAI chat client built on the worker from `OPENAI_API_KEY` and its own retries off. The worker checks for the key at start and exits with a one-line message when it is missing; if a factory throws anyway, the plugin fails that attempt with `MicrosoftAgentFrameworkConfigurationError`, logs the exception on the worker and retries the model Activity under its retry policy, so a worker fixed in place picks the run up.
- `new TemporalAgent("gpt-4o-mini", options)` in a `[Workflow]` class; the Workflow carries the model name and nothing else
- `await agent.RunAsync(prompt)` and `response.Text`, unchanged from Agent Framework

## Running the sample

Prerequisites: `OPENAI_API_KEY` in the worker's environment and a Temporal dev server (`temporal server start-dev`). Run both commands from this directory.

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
```

The starter prints `Result:` and the haiku. In the Temporal UI the Workflow `microsoft-agent-framework-hello-world` shows one `MicrosoftAgentFramework.InvokeModel` Activity with the summary `gpt-4o-mini`.

## Files

| File | Description |
|------|-------------|
| `HelloWorldWorkflow.workflow.cs` | `HelloWorldWorkflow` with one `TemporalAgent` |
| `Program.cs` | Builds the plugin with the OpenAI chat client factory, then runs the worker or the starter |
