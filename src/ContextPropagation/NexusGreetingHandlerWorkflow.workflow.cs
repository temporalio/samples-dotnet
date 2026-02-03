namespace TemporalioSamples.ContextPropagation;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class NexusGreetingHandlerWorkflow
{
    [WorkflowQuery]
    public string? CapturedUserId { get; private set; }

    [WorkflowRun]
    public async Task<INexusGreetingService.GreetingOutput> RunAsync(
        INexusGreetingService.GreetingInput input)
    {
        // Capture context to prove propagation through Nexus to handler workflow
        CapturedUserId = MyContext.UserId;
        Workflow.Logger.LogInformation(
            "Handler workflow executing for {Name}, called by user {UserId}",
            input.Name,
            CapturedUserId);

        var message = $"Greeting for {input.Name} (processed by user: {CapturedUserId})";
        return await Task.FromResult(new INexusGreetingService.GreetingOutput(message));
    }
}
