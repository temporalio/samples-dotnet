namespace TemporalioSamples.WorkflowStreams.ReconnectingSubscriber;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class PipelineWorkflow
{
    private readonly WorkflowStream stream;

    [WorkflowInit]
    public PipelineWorkflow(PipelineInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(PipelineInput input)
    {
        var status = stream.Topic(Constants.TopicStatus);
        var stageInterval = input.StageInterval ?? TimeSpan.FromSeconds(2);
        var stages = new[]
        {
            "validating",
            "loading data",
            "transforming",
            "writing output",
            "verifying",
            "complete",
        };
        foreach (var stage in stages)
        {
            status.Publish(new StageEvent(stage));
            if (stage != "complete")
            {
                await Workflow.DelayAsync(stageInterval);
            }
        }

        await Workflow.DelayAsync(Constants.DrainDelay);
        return $"pipeline {input.PipelineId} done";
    }
}
