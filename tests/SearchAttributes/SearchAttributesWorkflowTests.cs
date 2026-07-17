namespace TemporalioSamples.Tests.SearchAttributes;

using Temporalio.Common;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.SearchAttributes;
using Xunit;
using Xunit.Abstractions;

public class SearchAttributesWorkflowTests : TestBase
{
    private static readonly string[] ExpectedKeywordListValues = { "value1", "value2" };

    public SearchAttributesWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task RunAsync_SearchAttributesWorkflow_UpsertsAndQueries()
    {
        // Start a dev server with the custom search attributes registered
        await using var env = await WorkflowEnvironment.StartLocalAsync(new()
        {
            SearchAttributes =
            [
                SearchAttributesWorkflow.CustomIntField,
                SearchAttributesWorkflow.CustomKeywordField,
                SearchAttributesWorkflow.CustomBoolField,
                SearchAttributesWorkflow.CustomDoubleField,
                SearchAttributesWorkflow.CustomDatetimeField,
                SearchAttributesWorkflow.CustomKeywordListField,
            ],
        });

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions(taskQueue).
                AddAllActivities(new SearchAttributesActivities(env.Client)).
                AddWorkflow<SearchAttributesWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Run the workflow to completion. It reads the initial attribute, upserts more,
            // unsets one, and waits until it can find itself with a visibility query.
            var handle = await env.Client.StartWorkflowAsync(
                (SearchAttributesWorkflow wf) => wf.RunAsync(),
                new(id: $"search-attributes-{Guid.NewGuid()}", taskQueue: taskQueue)
                {
                    TypedSearchAttributes = new SearchAttributeCollection.Builder().
                        Set(SearchAttributesWorkflow.CustomIntField, 1).
                        ToSearchAttributeCollection(),
                });
            await handle.GetResultAsync();

            // Confirm the final search attribute values on the execution
            var description = await handle.DescribeAsync();
            var attributes = description.TypedSearchAttributes;
            Assert.Equal(2, attributes.Get(SearchAttributesWorkflow.CustomIntField));
            Assert.Equal("Keyword fields supports prefix search", attributes.Get(SearchAttributesWorkflow.CustomKeywordField));
            Assert.True(attributes.Get(SearchAttributesWorkflow.CustomBoolField));
            Assert.True(attributes.ContainsKey(SearchAttributesWorkflow.CustomDatetimeField));
            Assert.Equal(ExpectedKeywordListValues, attributes.Get(SearchAttributesWorkflow.CustomKeywordListField));

            // CustomDoubleField was unset by the workflow
            Assert.False(attributes.ContainsKey(SearchAttributesWorkflow.CustomDoubleField));
        });
    }
}
