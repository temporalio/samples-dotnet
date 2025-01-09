namespace TemporalioSamples.Tests;

using System;
using Temporalio.Client;
using Xunit;

public class WorkflowEnvironment : IAsyncLifetime
{
    private Temporalio.Testing.WorkflowEnvironment? env;

    public ITemporalClient Client =>
        env?.Client ?? throw new InvalidOperationException("Environment not created");

    public async Task InitializeAsync()
    {
        env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(new()
        {
            DevServerOptions = new()
            {
                ExtraArgs =
                [
                    "--dynamic-config-value",
                    "frontend.enableUpdateWorkflowExecution=true",
                    // Enable multi-op
                    "--dynamic-config-value",
                    "frontend.enableExecuteMultiOperation=true"
                ],
            },
        });
    }

    public async Task DisposeAsync()
    {
        if (env != null)
        {
            await env.ShutdownAsync();
        }
    }
}
