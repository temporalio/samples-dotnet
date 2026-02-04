# Temporal .NET SDK Samples

This is the set of .NET samples for the [.NET SDK](https://github.com/temporalio/sdk-dotnet).

## Usage

Prerequisites:

* .NET 8
* [Local Temporal server running](https://learn.temporal.io/getting_started/dotnet/dev_environment/)

## Samples

<!-- Keep this list in alphabetical order -->
* [ActivityHeartbeatingCancellation](src/ActivityHeartbeatingCancellation) - How to use heartbeating and cancellation handling in an activity.
* [ActivitySimple](src/ActivitySimple) - Simple workflow that runs simple activities.
* [ActivityWorker](src/ActivityWorker) - Use .NET activities from a workflow in another language.
* [AspNet](src/AspNet) - Demonstration of a generic host worker and an ASP.NET workflow starter.
* [Bedrock](src/Bedrock) - Orchestrate a chatbot with Amazon Bedrock.
* [ClientMtls](src/ClientMtls) - How to use client certificate authentication, e.g. for Temporal Cloud.
* [ContextPropagation](src/ContextPropagation) - Context propagation via interceptors.
* [CounterInterceptor](src/CounterInterceptor/) - Simple Workflow and Client Interceptors example.
* [DependencyInjection](src/DependencyInjection) - How to inject dependencies in activities and use generic hosts for workers
* [Dsl](src/Dsl) - Workflow that interprets and executes workflow steps from a YAML-based DSL.
* [EagerWorkflowStart](src/EagerWorkflowStart) - Demonstrates usage of Eager Workflow Start to reduce latency for workflows that start with a local activity.
* [Encryption](src/Encryption) - End-to-end encryption with Temporal payload codecs.
* [EnvConfig](src/EnvConfig) - Load client configuration from TOML files with programmatic overrides
* [Mutex](src/Mutex) - How to implement a mutex as a workflow. Demonstrates how to avoid race conditions or parallel mutually exclusive operations on the same resource.
* [NexusCancellation](src/NexusCancellation) - Demonstrates how to cancel a running Nexus operation from a caller workflow.
* [NexusContextPropagation](src/NexusContextPropagation) - Context propagation through Nexus services.
* [NexusMultiArg](src/NexusMultiArg) - Nexus service implementation calling a workflow with multiple arguments.
* [NexusSimple](src/NexusSimple) - Simple Nexus service implementation.
* [OpenTelemetry](src/OpenTelemetry) - Demonstrates how to set up OpenTelemetry tracing and metrics for both the client and worker, using both the .NET metrics API and internal forwarding from the Core SDK.
* [Patching](src/Patching) - Alter workflows safely with Patch and DeprecatePatch.
* [Polling](src/Polling) - Recommended implementation of an activity that needs to periodically poll an external resource waiting its successful completion.
* [SafeMessageHandlers](src/SafeMessageHandlers) - Use `Semaphore` to ensure operations are atomically processed in a workflow.
* [Saga](src/Saga) - Demonstrates how to implement a saga pattern.
* [Schedules](src/Schedules) - How to schedule workflows to be run at specific times in the future.
* [SignalsQueries](src/SignalsQueries) - A loyalty program using Signals and Queries.
* [SleepForDays](src/SleepForDays/) - Use a timer to send an email every 30 days.
* [Timer](src/Timer) - Use a timer to implement a monthly subscription; handle workflow cancellation.
* [UpdatableTimer](src/UpdatableTimer) - A timer that can be updated while sleeping.
* [UpdateWithStartEarlyReturn](src/UpdateWithStartEarlyReturn) - Use update with start to get an early return, letting the rest of the workflow complete in the background.
* [UpdateWithStartLazyInit](src/UpdateWithStartLazyInit) - Use update with start to lazily start a workflow before sending update.
* [WorkerSpecificTaskQueues](src/WorkerSpecificTaskQueues) - Use a unique task queue per Worker to have certain Activities only run on that specific Worker.
* [WorkerVersioning](src/WorkerVersioning) - How to use the Worker Versioning feature to more easily deploy changes to Workflow & other code.
* [WorkflowUpdate](src/WorkflowUpdate) - How to use the Workflow Update feature while blocking in update method for concurrent updates.

## Development

### Code formatting

This project uses StyleCop analyzers with some overrides in `.editorconfig`. To format, run:

    dotnet format

Can also run with `--verify-no-changes` to ensure it is formatted.

#### VisualStudio Code

When developing in vscode, the following JSON settings will enable StyleCop analyzers:

```json
    "omnisharp.enableEditorConfigSupport": true,
    "omnisharp.enableRoslynAnalyzers": true
```

### Testing

Run:

    dotnet test

Can add options like:

* `--logger "console;verbosity=detailed"` to show logs
* `--filter "FullyQualifiedName=TemporalioSamples.Tests.ActivityWorker.ActivityWorkerTests.Main_RunActivity_Succeeds"`
  to run a specific test

There is also a standalone project for running tests so output is more visible. To use it, run
`dotnet run --project tests/TemporalioSamples.Tests.csproj` and can pass options after `--`, e.g. `-- -verbose` and/or
`-- -method "*.RunAsync_SimpleRun_SucceedsAfterRetry"`.