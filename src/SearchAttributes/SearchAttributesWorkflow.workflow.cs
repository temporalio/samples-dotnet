using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace TemporalioSamples.SearchAttributes;

[Workflow]
public class SearchAttributesWorkflow
{
    public static readonly SearchAttributeKey<long> CustomIntField = SearchAttributeKey.CreateLong("CustomIntField");
    public static readonly SearchAttributeKey<string> CustomKeywordField = SearchAttributeKey.CreateKeyword("CustomKeywordField");
    public static readonly SearchAttributeKey<bool> CustomBoolField = SearchAttributeKey.CreateBool("CustomBoolField");
    public static readonly SearchAttributeKey<double> CustomDoubleField = SearchAttributeKey.CreateDouble("CustomDoubleField");
    public static readonly SearchAttributeKey<DateTimeOffset> CustomDatetimeField = SearchAttributeKey.CreateDateTimeOffset("CustomDatetimeField");
    public static readonly SearchAttributeKey<IReadOnlyCollection<string>> CustomKeywordListField = SearchAttributeKey.CreateKeywordList("CustomKeywordListField");

    private static readonly string[] KeywordListValues = { "value1", "value2" };

    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Logger.LogInformation("SearchAttributes workflow started");

        // Read the value set by the starter, failing the workflow if not set.
        if (!Workflow.TypedSearchAttributes.TryGetValue(CustomIntField, out var currentIntValue))
        {
            throw new ApplicationFailureException("Expected CustomIntField to be set on start");
        }

        Workflow.Logger.LogInformation("Initial search attribute value. CustomIntField: {CustomIntField}", currentIntValue);

        // Upsert search attributes. The local view is updated immediately, but the values are only
        // persisted on the server when the workflow task completes.
        Workflow.UpsertTypedSearchAttributes(
            CustomIntField.ValueSet(2), // Update CustomIntField from 1 to 2 and insert other fields
            CustomKeywordField.ValueSet("Keyword fields supports prefix search"),
            CustomBoolField.ValueSet(true),
            CustomDoubleField.ValueSet(3.14),
            CustomDatetimeField.ValueSet(new(Workflow.UtcNow)),
            CustomKeywordListField.ValueSet(KeywordListValues));
        PrintSearchAttributes();

        // Unset values with ValueUnset.
        Workflow.UpsertTypedSearchAttributes(CustomDoubleField.ValueUnset());
        PrintSearchAttributes();

        // Yield so the upsert commands are sent to the server and the visibility store can update.
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));

        // Query the visibility store for this workflow using a couple of attributes set above. The
        // activity polls until the current execution is indexed since visibility is eventually
        // consistent.
        var query = $"{CustomIntField.Name}=2 AND {CustomKeywordField.Name} STARTS_WITH 'Keyword fields'";
        var lastExecution = await Workflow.ExecuteActivityAsync(
            (SearchAttributesActivities acts) => acts.WaitForFirstMatchingExecutionAsync(query),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                HeartbeatTimeout = TimeSpan.FromSeconds(5),
            });

        // Logs that show the current execution is the same as the one returned from the
        // visibility store.
        Workflow.Logger.LogInformation(
            "WorkflowId must be the same. Current: {WorkflowId}, from visibility query: {QueriedWorkflowId}",
            Workflow.Info.WorkflowId,
            lastExecution.WorkflowId);
        Workflow.Logger.LogInformation(
            "RunId must be the same. Current: {RunId}, from visibility query: {QueriedRunId}",
            Workflow.Info.RunId,
            lastExecution.RunId);

        // No change to any attributes should be expected since the last print call.
        PrintSearchAttributes();

        Workflow.Logger.LogInformation("Workflow completed");
    }

    private static void PrintSearchAttributes()
    {
        foreach (var pair in Workflow.TypedSearchAttributes.UntypedValues.OrderBy(pair => pair.Key.Name, StringComparer.Ordinal))
        {
            Workflow.Logger.LogInformation("Current search attribute value. {Name}: {Value}", pair.Key.Name, pair.Value);
        }
    }
}
