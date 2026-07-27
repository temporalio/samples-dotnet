# Nexus Dependency Injection

This sample shows how to configure a Nexus service handler for dependency injection using the
`Temporalio.Extensions.Hosting` generic-host worker.

The handler (`GreetingServiceHandler`) has an `IGreetingClient` injected via its constructor and is
registered on the worker with `AddScopedNexusService`. Nexus services can also be registered as
singletons or transients via `AddSingletonNexusService` and `AddTransientNexusService`, mirroring
`AddScopedActivities` / `AddSingletonActivities` / `AddTransientActivities`.

The caller workflow and the handler connect to two different namespaces (a "caller" namespace and a
"handler" namespace) — this mirrors how Nexus is typically used to cross namespace boundaries. The
client is configured via the SDK's [environment configuration](https://docs.temporal.io/develop/environment-configuration)
support (`ClientEnvConfig.LoadClientConnectOptions()`), which reads `TEMPORAL_NAMESPACE`,
`TEMPORAL_ADDRESS`, etc. from the environment (and optionally profiles from `temporal.toml`).

## Run locally against a dev server

1. Start the Temporal dev server with the required namespaces pre-created:

   ```bash
   temporal server start-dev \
     --namespace my-caller-namespace \
     --namespace my-handler-namespace
   ```

2. Create a Nexus endpoint that routes to the handler namespace and the worker's task queue:

   ```bash
   temporal operator nexus endpoint create \
     --name my-nexus-endpoint \
     --target-namespace my-handler-namespace \
     --target-task-queue nexus-handler-queue
   ```

3. In a second terminal, start the handler worker in the handler namespace:

   ```bash
   TEMPORAL_NAMESPACE=my-handler-namespace dotnet run handler-worker
   ```

4. In a third terminal, start the caller worker in the caller namespace:

   ```bash
   TEMPORAL_NAMESPACE=my-caller-namespace dotnet run caller-worker
   ```

5. In a fourth terminal, run the caller workflow in the caller namespace:

   ```bash
   TEMPORAL_NAMESPACE=my-caller-namespace dotnet run caller-workflow
   ```

   The workflow calls the Nexus operation, whose handler uses the injected dependency, and prints a
   result similar to:

   ```
   Workflow result: Hello, Temporal 👋
   ```

## Run against Temporal Cloud

1. Create two namespaces in Temporal Cloud (for example `my-caller-namespace.<account>` and
   `my-handler-namespace.<account>`) and generate an API key (or mTLS cert) that can access both.

2. Create a Nexus endpoint that targets the handler namespace and the worker's task queue. See the
   Temporal Cloud instructions at https://docs.temporal.io/nexus/registry#create-a-nexus-endpoint.
   Use:
   - Endpoint name: `my-nexus-endpoint`
   - Target namespace: `my-handler-namespace.<account>`
   - Target task queue: `nexus-handler-queue`
   - Allowed caller namespaces: include `my-caller-namespace.<account>` (endpoints reject callers
     that are not on this list)

3. Add two profiles to your [environment configuration file](https://docs.temporal.io/develop/environment-configuration),
   one per namespace. Using API keys:

   ```toml
   [profile.handler]
   address = "<region>.<cloud>.api.temporal.io:7233"
   namespace = "my-handler-namespace.<account>"
   api_key = "<your-api-key>"

   [profile.caller]
   address = "<region>.<cloud>.api.temporal.io:7233"
   namespace = "my-caller-namespace.<account>"
   api_key = "<your-api-key>"
   ```

   For mTLS instead of API keys, set `tls.client_cert_path` and `tls.client_key_path` on each profile
   (see the [docs](https://docs.temporal.io/develop/environment-configuration) for the full schema).

4. Run the workers and the workflow in separate terminals from this directory, selecting the
   appropriate profile in each:

   ```bash
   # terminal 1 (handler worker, handler namespace)
   TEMPORAL_PROFILE=handler dotnet run handler-worker
   ```

   ```bash
   # terminal 2 (caller worker, caller namespace)
   TEMPORAL_PROFILE=caller dotnet run caller-worker
   ```

   ```bash
   # terminal 3 (caller workflow, caller namespace)
   TEMPORAL_PROFILE=caller dotnet run caller-workflow
   ```
