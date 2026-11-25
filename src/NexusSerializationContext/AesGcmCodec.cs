namespace TemporalioSamples.NexusSerializationContext;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

public sealed class AesGcmCodec : IPayloadCodec, IWithSerializationContext<IPayloadCodec>
{
    private readonly string keyId;
    private readonly byte[] key;
    private readonly bool compress;
    private readonly string? endpoint;

    public AesGcmCodec(string keyId, string key, bool compress = false)
        : this(keyId, Encoding.UTF8.GetBytes(key), compress, null)
    {
    }

    private AesGcmCodec(string keyId, byte[] key, bool compress, string? endpoint)
    {
        this.keyId = keyId;
        this.key = key;
        this.compress = compress;
        this.endpoint = endpoint;
    }

    public IPayloadCodec WithSerializationContext(ISerializationContext context) =>
        new AesGcmCodec(keyId, key, compress, (context as ISerializationContext.Nexus)?.Endpoint);

    public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(Encode).ToArray());

    public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(Decode).ToArray());

    private Payload Encode(Payload payload)
    {
        if (compress)
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
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(bytes.AsSpan(0, 12), plaintext, bytes.AsSpan(12, plaintext.Length), bytes.AsSpan(12 + plaintext.Length));
        var result = new Payload
        {
            Metadata =
            {
                ["encoding"] = ByteString.CopyFromUtf8("binary/nexus-aes-gcm"),
                ["encryption-key-id"] = ByteString.CopyFromUtf8(keyId),
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
        if (payload.Metadata.GetValueOrDefault("encoding")?.ToStringUtf8() != "binary/nexus-aes-gcm")
        {
            return payload;
        }
        if (payload.Metadata.GetValueOrDefault("encryption-key-id")?.ToStringUtf8() != keyId)
        {
            throw new InvalidOperationException("Unexpected encryption key ID");
        }
        var bytes = payload.Data.ToByteArray();
        if (bytes.Length < 28)
        {
            throw new CryptographicException("Truncated AES-GCM envelope");
        }
        var plaintext = new byte[bytes.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(12, plaintext.Length), bytes.AsSpan(bytes.Length - 16), plaintext);
        var result = Payload.Parser.ParseFrom(plaintext);
        if (compress)
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
