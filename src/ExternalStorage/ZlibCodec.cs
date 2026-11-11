namespace TemporalioSamples.ExternalStorage;

using System.IO.Compression;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

/// <summary>
/// Compresses each payload, so the size compared against the external storage threshold is the
/// compressed size.
/// </summary>
public sealed class ZlibCodec : IPayloadCodec
{
    private static readonly ByteString ZlibEncoding = ByteString.CopyFromUtf8("binary/zlib");

    public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            {
                p.WriteTo(zlib);
            }
            return new Payload
            {
                Metadata = { ["encoding"] = ZlibEncoding },
                Data = ByteString.CopyFrom(output.ToArray()),
            };
        }).ToList());

    public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
        {
            if (p.Metadata.GetValueOrDefault("encoding") != ZlibEncoding)
            {
                return p;
            }
            using var zlib = new ZLibStream(new MemoryStream(p.Data.ToByteArray()), CompressionMode.Decompress);
            return Payload.Parser.ParseFrom(zlib);
        }).ToList());
}
