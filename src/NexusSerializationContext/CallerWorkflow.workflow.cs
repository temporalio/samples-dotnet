namespace TemporalioSamples.NexusSerializationContext;

using Temporalio.Workflows;

[Workflow]
public class CallerWorkflow
{
    [WorkflowRun]
    public async Task<EndpointResults[]> RunAsync(string message)
    {
        var starts = SampleConfig.Endpoints.Select(async endpoint =>
        {
            var syncStart = Workflow.CreateNexusWorkflowClient<ISyncEchoService>(endpoint).
                StartNexusOperationAsync(service => service.Echo(message), new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(30) });
            var asyncStart = Workflow.CreateNexusWorkflowClient<IAsyncEchoService>(endpoint).
                StartNexusOperationAsync(service => service.Echo(message), new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(30) });
            return (endpoint, sync: await syncStart, asyncOperation: await asyncStart);
        }).ToArray();
        // Start all eight operations before waiting for any results.
        var handles = await Task.WhenAll(starts);
        var results = new List<EndpointResults>();
        foreach (var (endpoint, sync, asyncOperation) in handles)
        {
            results.Add(new(endpoint, await sync.GetResultAsync(), await asyncOperation.GetResultAsync()));
        }
        return results.ToArray();
    }
}

public record EndpointResults(string Endpoint, string Sync, string Async);
