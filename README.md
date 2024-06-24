# Temporal .NET SDK Samples

This is the set of .NET samples for the [.NET SDK](https://github.com/temporalio/sdk-dotnet).

## Usage

Prerequisites:

* .NET 8
* [Local Temporal server running](https://docs.temporal.io/application-development/foundations#run-a-development-cluster)

## Samples

<!-- Keep this list in alphabetical order -->
* [ActivityHeartbeatingCancellation](src/ActivityHeartbeatingCancellation) - How to use heartbeating and cancellation handling in an activity.
* [ActivitySimple](src/ActivitySimple) - Simple workflow that runs simple activities.
* [ActivityWorker](src/ActivityWorker) - Use .NET activities from a workflow in another language.
* [AtomicMessageHandlers](src/AtomicMessageHandlers) - Use `SemaphoreSlim` to ensure operations are atomically processed in a workflow.
* [AspNet](src/AspNet) - Demonstration of a generic host worker and an ASP.NET workflow starter.
* [ClientMtls](src/ClientMtls) - How to use client certificate authentication, e.g. for Temporal Cloud.
* [ContextPropagation](src/ContextPropagation) - Context propagation via interceptors.
* [DependencyInjection](src/DependencyInjection) - How to inject dependencies in activities and use generic hosts for workers
* [Encryption](src/Encryption) - End-to-end encryption with Temporal payload codecs.
* [Mutex](src/Mutex) - How to implement a mutex as a workflow. Demonstrates how to avoid race conditions or parallel mutually exclusive operations on the same resource.
* [Polling](src/Polling) - Recommended implementation of an activity that needs to periodically poll an external resource waiting its successful completion.
* [Saga](src/Saga) - Demonstrates how to implement a saga pattern.
* [Schedules](src/Schedules) - How to schedule workflows to be run at specific times in the future.
* [SignalsQueries](src/SignalsQueries) - A loyalty program using Signals and Queries.
* [Timer](src/Timer) - Use a timer to implement a monthly subscription; handle workflow cancellation.
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