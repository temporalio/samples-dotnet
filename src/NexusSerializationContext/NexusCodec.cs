namespace TemporalioSamples.NexusSerializationContext;

using System.Collections.Frozen;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

public sealed record EndpointEncoding(string KeyId, string Key, bool Compress);

public sealed class NexusCodec : IPayloadCodec, IWithSerializationContext<IPayloadCodec>
{
    private readonly FrozenDictionary<string, EndpointEncoding> endpoints;
    private readonly EndpointEncoding workflow;
    private readonly string? endpoint;

    public NexusCodec(FrozenDictionary<string, EndpointEncoding> endpoints, EndpointEncoding workflow)
        : this(endpoints, workflow, null)
    {
    }

    private NexusCodec(FrozenDictionary<string, EndpointEncoding> endpoints, EndpointEncoding workflow, string? endpoint)
    {
        this.endpoints = endpoints;
        this.workflow = workflow;
        this.endpoint = endpoint;
    }

    public IPayloadCodec WithSerializationContext(ISerializationContext context) =>
        // SDK instances are shared across operations: bind a cheap wrapper without mutating their configuration.
        new NexusCodec(endpoints, workflow, (context as ISerializationContext.Nexus)?.Endpoint);

    public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(Encode).ToArray());

    public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(Decode).ToArray());

    private EndpointEncoding Selected => endpoint == null ? workflow :
        endpoints.TryGetValue(endpoint, out var encoding) ? encoding :
        throw new InvalidOperationException($"Unknown Nexus endpoint: {endpoint}");

    private Payload Encode(Payload payload)
    {
        var selected = Selected;
        if (selected.Compress)
        {
            using var stream = new MemoryStream();
            using (var zlib = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                payload.WriteTo(zlib);
            }
            payload = new Payload
            {
                Metadata = { ["encoding"] = ByteString.CopyFromUtf8("binary/nexus-zlib") },
                Data = ByteString.CopyFrom(stream.ToArray()),
            };
        }
        var plaintext = payload.ToByteArray();
        var bytes = new byte[12 + plaintext.Length + 16];
        RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
        using var aes = new AesGcm(Encoding.UTF8.GetBytes(selected.Key), 16);
        aes.Encrypt(bytes.AsSpan(0, 12), plaintext, bytes.AsSpan(12, plaintext.Length), bytes.AsSpan(12 + plaintext.Length));
        var result = new Payload
        {
            Metadata =
            {
                ["encoding"] = ByteString.CopyFromUtf8("binary/nexus-aes-gcm"),
                ["encryption-key-id"] = ByteString.CopyFromUtf8(selected.KeyId),
            },
            Data = ByteString.CopyFrom(bytes),
        };
        if (endpoint != null)
        {
            result.Metadata["nexus-endpoint-name"] = ByteString.CopyFromUtf8(endpoint);
        }
        return result;
    }

    private Payload Decode(Payload payload)
    {
        var selected = Selected;
        if (payload.Metadata.GetValueOrDefault("encoding")?.ToStringUtf8() != "binary/nexus-aes-gcm")
        {
            return payload;
        }
        if (payload.Metadata.GetValueOrDefault("encryption-key-id")?.ToStringUtf8() != selected.KeyId)
        {
            throw new InvalidOperationException("Unexpected encryption key ID");
        }
        var bytes = payload.Data.ToByteArray();
        if (bytes.Length < 28)
        {
            throw new CryptographicException("Truncated AES-GCM envelope");
        }
        var plaintext = new byte[bytes.Length - 28];
        using var aes = new AesGcm(Encoding.UTF8.GetBytes(selected.Key), 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(12, plaintext.Length), bytes.AsSpan(bytes.Length - 16), plaintext);
        var result = Payload.Parser.ParseFrom(plaintext);
        if (selected.Compress)
        {
            if (result.Metadata.GetValueOrDefault("encoding")?.ToStringUtf8() != "binary/nexus-zlib")
            {
                throw new InvalidOperationException("Expected a Nexus zlib payload");
            }
            using var stream = new MemoryStream(result.Data.ToByteArray());
            using var zlib = new ZLibStream(stream, CompressionMode.Decompress);
            return Payload.Parser.ParseFrom(zlib);
        }
        return result;
    }
}
