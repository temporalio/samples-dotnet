namespace TemporalioSamples.Tests.Dsl;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.Dsl;
using Xunit;
using Xunit.Abstractions;

public class DslWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
    : WorkflowEnvironmentTestBase(output, env)
{
    [Fact]
    public async Task RunAsync_Workflow1_SimpleSequence_Succeeds()
    {
        const string yaml =
            """
            variables:
              arg1: value1
              arg2: value2
            
            root:
              sequence:
                elements:
                  - activity:
                      name: activity1
                      arguments:
                        - arg1
                      result: result1
                  - activity:
                      name: activity2
                      arguments:
                        - result1
                      result: result2
                  - activity:
                      name: activity3
                      arguments:
                        - arg2
                        - result2
                      result: result3
            """;

        var input = DslInput.Parse(yaml);

        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"dsl-test-{Guid.NewGuid()}")
                .AddAllActivities(typeof(DslActivities), null)
                .AddWorkflow<DslWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var result = await Client.ExecuteWorkflowAsync(
                (DslWorkflow wf) => wf.RunAsync(input),
                new(id: $"dsl-workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            Assert.Equal("value1", result["arg1"].ToString());
            Assert.Equal("value2", result["arg2"].ToString());
            Assert.Equal("[result from activity1: value1]", result["result1"].ToString());
            Assert.Equal("[result from activity2: [result from activity1: value1]]", result["result2"].ToString());
            Assert.Equal("[result from activity3: value2 [result from activity2: [result from activity1: value1]]]", result["result3"].ToString());
        });
    }

    [Fact]
    public async Task RunAsync_Workflow2_ReturnsExpectedVariables()
    {
        const string yaml =
            """
            variables:
              arg1: value1
              arg2: value2
              arg3: value3
            
            root:
              sequence:
                elements:
                  - activity:
                      name: activity1
                      arguments:
                        - arg1
                      result: result1
                  - parallel:
                      branches:
                        - sequence:
                            elements:
                              - activity:
                                  name: activity2
                                  arguments:
                                    - result1
                                  result: result2
                              - activity:
                                  name: activity3
                                  arguments:
                                    - arg2
                                    - result2
                                  result: result3
                        - sequence:
                            elements:
                              - activity:
                                  name: activity4
                                  arguments:
                                    - result1
                                  result: result4
                              - activity:
                                  name: activity5
                                  arguments:
                                    - arg3
                                    - result4
                                  result: result5
                  - activity:
                      name: activity3
                      arguments:
                        - result3
                        - result5
                      result: result6
            """;

        var input = DslInput.Parse(yaml);

        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"dsl-test-{Guid.NewGuid()}")
                .AddAllActivities(typeof(DslActivities), null)
                .AddWorkflow<DslWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (DslWorkflow wf) => wf.RunAsync(input),
                new(id: $"dsl-workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            var result = await handle.GetResultAsync();

            Assert.Equal("value1", result["arg1"].ToString());
            Assert.Equal("value2", result["arg2"].ToString());
            Assert.Equal("value3", result["arg3"].ToString());

            Assert.Equal("[result from activity1: value1]", result["result1"].ToString());
            Assert.Equal("[result from activity2: [result from activity1: value1]]", result["result2"].ToString());
            Assert.Equal("[result from activity3: value2 [result from activity2: [result from activity1: value1]]]", result["result3"].ToString());
            Assert.Equal("[result from activity4: [result from activity1: value1]]", result["result4"].ToString());
            Assert.Equal("[result from activity5: value3 [result from activity4: [result from activity1: value1]]]", result["result5"].ToString());
            Assert.Equal(
                "[result from activity3: [result from activity3: value2 [result from activity2: " +
                "[result from activity1: value1]]] [result from activity5: " +
                "value3 [result from activity4: [result from activity1: value1]]]]",
                result["result6"].ToString());

            // Collect all activity events and confirm they are in order expected
            var history = await handle.FetchHistoryAsync();
            var activityNames = history.Events
                .Where(e => e.EventType == EventType.ActivityTaskScheduled)
                .Select(e => e.ActivityTaskScheduledEventAttributes.ActivityType.Name)
                .ToList();

            Assert.Equal(6, activityNames.Count);
            Assert.Equal("activity1", activityNames[0]);
            Assert.Equal(["activity2", "activity3", "activity4", "activity5"], activityNames.Skip(1).Take(4).Order().ToList());
            Assert.Equal("activity3", activityNames[5]);
        });
    }
}
