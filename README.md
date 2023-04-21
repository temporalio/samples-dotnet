# Temporal .NET SDK Samples

This is the set of .NET samples for the [.NET SDK](https://github.com/temporalio/sdk-dotnet).

⚠️ UNDER ACTIVE DEVELOPMENT

The .NET SDK and is under active development and has not released a stable version yet. APIs may change in incompatible
ways until the SDK is marked stable.

## Usage

Prerequisites:

* .NET >= 6
* [Local Temporal server running](https://docs.temporal.io/application-development/foundations#run-a-development-cluster)

## Samples

<!-- Keep this list in alphabetical order -->
* [ActivitySimple](src/ActivitySimple) - Simple workflow that runs simple activities.
* [ActivityWorker](src/ActivityWorker) - Use .NET activities from a workflow in another language.
* [AspNet](src/AspNet) - Demonstration of a generic host worker and an ASP.NET workflow starter.
* [ClientMtls](src/ClientMtls) - How to use client certificate authentication, e.g. for Temporal Cloud.
* [Encryption](src/Encryption) - End-to-end encryption with Temporal payload codecs.

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