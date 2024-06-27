namespace TemporalioSamples.SafeMessageHandlers;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class ClusterManagerActivities
{
    public record AllocateNodesToJobInput(
        IList<string> Nodes,
        string JobName);

    [Activity]
    public async Task AllocateNodesToJobAsync(AllocateNodesToJobInput input)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Assigning nodes {Nodes} to job {TaskName}", input.Nodes, input.JobName);
        await Task.Delay(100);
    }

    public record DeallocateNodesFromJobInput(
        IList<string> Nodes,
        string JobName);

    [Activity]
    public async Task DeallocateNodesFromJobAsync(DeallocateNodesFromJobInput input)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Deallocating nodes {Nodes} from job {TaskName}", input.Nodes, input.JobName);
        await Task.Delay(100);
    }

    public record FindBadNodesInput(
        IList<string> Nodes);

    [Activity]
    public async Task<List<string>> FindBadNodesAsync(FindBadNodesInput input)
    {
        await Task.Delay(100);
        return input.Nodes.
            Select((node, index) => index % 5 == 0 ? null : node).
            OfType<string>().
            ToList();
    }
}