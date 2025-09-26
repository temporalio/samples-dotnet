# Environment Configuration

This sample demonstrates how to use the Temporal SDK's external client configuration feature. This feature allows you to configure a `TemporalClient` using a TOML file and/or programmatic overrides, decoupling connection settings from your application code.

To run, first see [README.md](../../README.md) for prerequisites.

## Configuration File

The `config.toml` file defines three profiles for different environments:

- `[profile.default]`: A working configuration for local development.
- `[profile.staging]`: A configuration with an intentionally **incorrect** address (`localhost:9999`) to demonstrate how it can be corrected by an override.
- `[profile.prod]`: A non-runnable, illustrative-only configuration showing a realistic setup for Temporal Cloud with placeholder credentials. This profile is not used by the samples but serves as a reference.

## Running the Samples

The following commands demonstrate different ways to load and use these configuration profiles from this directory:

### Load from File

This sample shows the most common use case: loading the `default` profile from the `config.toml` file.

    dotnet run load-from-file

### Load Profile with Override

This sample demonstrates loading the `staging` profile by name (which has an incorrect address) and then correcting the address programmatically. This highlights the recommended approach for overriding configuration values at runtime.

    dotnet run load-profile

Both samples will attempt to connect to a local Temporal server and display connection information.