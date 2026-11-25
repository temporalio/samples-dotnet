namespace TemporalioSamples.NexusSerializationContext;

using System.Collections.Frozen;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

public sealed class NexusCodec : IPayloadCodec, IWithSerializationContext<IPayloadCodec>
{
    private readonly FrozenDictionary<string, IPayloadCodec> endpointCodecs;
    private readonly IPayloadCodec workflowCodec;
    private readonly ISerializationContext? context;

    public NexusCodec(FrozenDictionary<string, IPayloadCodec> endpointCodecs, IPayloadCodec workflowCodec)
        : this(endpointCodecs, workflowCodec, null)
    {
    }

    private NexusCodec(
        FrozenDictionary<string, IPayloadCodec> endpointCodecs, IPayloadCodec workflowCodec, ISerializationContext? context)
    {
        this.endpointCodecs = endpointCodecs;
        this.workflowCodec = workflowCodec;
        this.context = context;
    }

    private IPayloadCodec Selected
    {
        get
        {
            var codec = workflowCodec;
            if (context is ISerializationContext.Nexus nexus && !endpointCodecs.TryGetValue(nexus.Endpoint, out codec))
            {
                throw new InvalidOperationException($"Unknown Nexus endpoint: {nexus.Endpoint}");
            }

            return context != null && codec is IWithSerializationContext<IPayloadCodec> contextual
                ? contextual.WithSerializationContext(context)
                : codec;
        }
    }

    public IPayloadCodec WithSerializationContext(ISerializationContext context) =>
        new NexusCodec(endpointCodecs, workflowCodec, context);

    public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Selected.EncodeAsync(payloads);

    public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Selected.DecodeAsync(payloads);
}
