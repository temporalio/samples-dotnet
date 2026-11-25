namespace TemporalioSamples.NexusSerializationContext;

using System.Collections.Frozen;
using Temporalio.Converters;

public static class SampleConfig
{
    public const string Caller = "nexus-serialization-caller";
    public const string CompressedHandler = "nexus-serialization-compressed-encrypted-handler";
    public const string EncryptedHandler = "nexus-serialization-encrypted-handler";

    public static readonly IReadOnlyList<string> Endpoints = Array.AsReadOnly(new[]
    {
        "nexus-serialization-compressed-encrypted-1",
        "nexus-serialization-compressed-encrypted-2",
        "nexus-serialization-encrypted-1",
        "nexus-serialization-encrypted-2",
    });

    public static DataConverter CallerConverter() => CreateConverter(null);

    public static DataConverter HandlerConverter(bool compressed) => CreateConverter(compressed);

    private static DataConverter CreateConverter(bool? compressed)
    {
        var allEndpoints = new Dictionary<string, EndpointEncoding>
        {
            [Endpoints[0]] = new("compressed-encrypted-1", "handler-a-key-01", true),
            [Endpoints[1]] = new("compressed-encrypted-2", "handler-a-key-02", true),
            [Endpoints[2]] = new("encrypted-1", "handler-b-key-01", false),
            [Endpoints[3]] = new("encrypted-2", "handler-b-key-02", false),
        };
        var endpointCodecs = allEndpoints.Where(e => compressed == null || e.Value.Compress == compressed).
            ToFrozenDictionary(e => e.Key, e => (IPayloadCodec)new AesGcmCodec(e.Value.KeyId, e.Value.Key, e.Value.Compress));
        var workflow = compressed == null
            ? new EndpointEncoding("caller-workflow", "sample-key-C-123", false)
            : allEndpoints[Endpoints[compressed.Value ? 0 : 2]];
        var workflowCodec = new AesGcmCodec(workflow.KeyId, workflow.Key);
        return DataConverter.Default with { PayloadCodec = new NexusCodec(endpointCodecs, workflowCodec) };
    }

    private sealed record EndpointEncoding(string KeyId, string Key, bool Compress);
}
