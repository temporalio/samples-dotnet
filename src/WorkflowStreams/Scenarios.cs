namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;

public static partial class Scenarios
{
    private static T Decode<T>(ITemporalClient client, WorkflowStreamItem item) =>
        client.Options.DataConverter.PayloadConverter.ToValue<T>(item.Payload);
}
