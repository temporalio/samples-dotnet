namespace TemporalioSamples.NexusSerializationContext;

using Temporalio.Workflows;

[Workflow]
public class CallerWorkflow
{
    [WorkflowRun]
    public Task<EndpointResults[]> RunAsync(string message) =>
        Task.WhenAll(SampleConfig.Endpoints.Select(async endpoint =>
        {
            var options = new NexusWorkflowOperationOptions { ScheduleToCloseTimeout = TimeSpan.FromSeconds(30) };
            // Start both calls before awaiting either result.
            var sync = Workflow.CreateNexusWorkflowClient<ISyncEchoService>(endpoint).
                ExecuteNexusOperationAsync(service => service.Echo(message), options);
            var asyncResult = Workflow.CreateNexusWorkflowClient<IAsyncEchoService>(endpoint).
                ExecuteNexusOperationAsync(service => service.Echo(message), options);
            return new EndpointResults(endpoint, await sync, await asyncResult);
        }));
}

public record EndpointResults(string Endpoint, string Sync, string Async);
