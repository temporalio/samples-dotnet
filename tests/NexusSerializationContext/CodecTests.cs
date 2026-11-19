namespace TemporalioSamples.Tests.NexusSerializationContext;

using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using TemporalioSamples.NexusSerializationContext;
using Xunit;

public class CodecTests
{
    [Fact]
    public async Task Endpoints_RoundTripWithTheirOwnKeysAsync()
    {
        foreach (var endpoint in SampleConfig.Endpoints)
        {
            var caller = Bind(SampleConfig.CallerConverter(), endpoint);
            var handler = Bind(SampleConfig.HandlerConverter(endpoint.Contains("compressed")), endpoint);
            var request = await caller.ToPayloadAsync("hello");
            AssertMetadata(request, endpoint);
            Assert.NotEmpty(Decrypt(request, RawKey(endpoint)).Data);
            Assert.Equal("hello", await handler.ToValueAsync<string>(request));
            var plain = DataConverter.Default.PayloadConverter.ToPayload("plain");
            Assert.Equal("plain", await handler.ToValueAsync<string>(plain));
            var reply = await handler.ToPayloadAsync("reply");
            AssertMetadata(reply, endpoint);
            Assert.NotEmpty(Decrypt(reply, RawKey(endpoint)).Data);
            Assert.Equal("reply", await caller.ToValueAsync<string>(reply));
        }
    }

    [Fact]
    public async Task Compression_BothKeysAsync()
    {
        foreach (var pair in new[] { (0, 2), (1, 3) })
        {
            var message = string.Concat(Enumerable.Repeat("repeat me ", 100));
            var compressed = await Bind(SampleConfig.CallerConverter(), SampleConfig.Endpoints[pair.Item1]).ToPayloadAsync(message);
            var encrypted = await Bind(SampleConfig.CallerConverter(), SampleConfig.Endpoints[pair.Item2]).ToPayloadAsync(message);
            Assert.True(compressed.Data.Length < encrypted.Data.Length);
            var inner = Decrypt(compressed, RawKey(SampleConfig.Endpoints[pair.Item1]));
            Assert.Equal("binary/nexus-zlib", inner.Metadata["encoding"].ToStringUtf8());
        }
    }

    [Fact]
    public async Task WorkflowDefaults_UseSeparateUncompressedKeysAsync()
    {
        foreach (var (converter, id, key) in new[]
        {
            (SampleConfig.CallerConverter(), "caller-workflow", "sample-key-C-123"),
            (SampleConfig.HandlerConverter(true), "compressed-encrypted-1", "handler-a-key-01"),
            (SampleConfig.HandlerConverter(false), "encrypted-1", "handler-b-key-01"),
        })
        {
            var bound = converter.WithSerializationContext(new ISerializationContext.Workflow("namespace", "id"));
            var payload = await bound.ToPayloadAsync("hello");
            Assert.Equal(id, payload.Metadata["encryption-key-id"].ToStringUtf8());
            Assert.False(payload.Metadata.ContainsKey("nexus-endpoint-name"));
            Assert.Equal("json/plain", Decrypt(payload, key).Metadata["encoding"].ToStringUtf8());
            Assert.Equal("hello", await bound.ToValueAsync<string>(payload));
        }
    }

    [Fact]
    public async Task UnknownEndpoint_IsRejectedAsync()
    {
        var converter = Bind(SampleConfig.CallerConverter(), "unknown");
        await Assert.ThrowsAsync<InvalidOperationException>(() => converter.ToPayloadAsync("hello"));
    }

    [Fact]
    public async Task Binding_RetainsEarlierBindingsAndClearsNexusContextAsync()
    {
        var original = SampleConfig.CallerConverter();
        var first = Bind(original, SampleConfig.Endpoints[2]);
        var second = Bind(original, SampleConfig.Endpoints[3]);
        var workflow = first.WithSerializationContext(new ISerializationContext.Workflow("caller", "id"));
        foreach (var (converter, endpoint) in new[] { (first, SampleConfig.Endpoints[2]), (second, SampleConfig.Endpoints[3]) })
        {
            var payload = await converter.ToPayloadAsync("hello");
            AssertMetadata(payload, endpoint);
            Assert.NotEmpty(Decrypt(payload, RawKey(endpoint)).Data);
        }
        foreach (var converter in new[] { original, workflow })
        {
            var payload = await converter.ToPayloadAsync("hello");
            Assert.Equal("caller-workflow", payload.Metadata["encryption-key-id"].ToStringUtf8());
            Assert.False(payload.Metadata.ContainsKey("nexus-endpoint-name"));
            Assert.NotEmpty(Decrypt(payload, "sample-key-C-123").Data);
        }
    }

    [Fact]
    public async Task RepeatedInput_UsesFreshNoncesAsync()
    {
        var converter = Bind(SampleConfig.CallerConverter(), SampleConfig.Endpoints[3]);
        var first = await converter.ToPayloadAsync("hello");
        var second = await converter.ToPayloadAsync("hello");
        Assert.NotEqual(first.Data.ToByteArray()[..12], second.Data.ToByteArray()[..12]);
        Assert.NotEqual(first.Data, second.Data);
        Assert.Equal("hello", await converter.ToValueAsync<string>(first));
        Assert.Equal("hello", await converter.ToValueAsync<string>(second));
    }

    [Fact]
    public async Task DamagedCiphertext_IsRejectedAsync()
    {
        var converter = Bind(SampleConfig.CallerConverter(), SampleConfig.Endpoints[3]);
        var original = await converter.ToPayloadAsync("hello");
        var altered = original.Data.ToByteArray();
        altered[12] ^= 1;
        foreach (var data in new[] { altered, altered[..1], original.Data.ToByteArray()[..^1] })
        {
            var damaged = original.Clone();
            damaged.Data = ByteString.CopyFrom(data);
            await Assert.ThrowsAnyAsync<CryptographicException>(() => converter.ToValueAsync<string>(damaged));
        }
    }

    [Fact]
    public async Task EncryptedPayloads_RequireExpectedKeyAndCompressionAsync()
    {
        var endpoint = SampleConfig.Endpoints[0];
        var compressed = Bind(SampleConfig.CallerConverter(), endpoint);
        var otherEndpoint = Bind(SampleConfig.CallerConverter(), SampleConfig.Endpoints[1]);
        var wrongKey = await otherEndpoint.ToPayloadAsync("hello");
        var keyError = await Assert.ThrowsAsync<InvalidOperationException>(() => compressed.ToValueAsync<string>(wrongKey));
        Assert.Equal("Unexpected encryption key ID", keyError.Message);

        var uncompressed = await SampleConfig.HandlerConverter(true).ToPayloadAsync("hello");
        var formatError = await Assert.ThrowsAsync<InvalidOperationException>(() => compressed.ToValueAsync<string>(uncompressed));
        Assert.Equal("Expected a Nexus zlib payload", formatError.Message);
    }

    private static DataConverter Bind(DataConverter converter, string endpoint) =>
        converter.WithSerializationContext(new ISerializationContext.Nexus(endpoint, "SyncEchoService", "echo"));

    private static string RawKey(string endpoint) =>
        endpoint switch
        {
            "nexus-serialization-compressed-encrypted-1" => "handler-a-key-01",
            "nexus-serialization-compressed-encrypted-2" => "handler-a-key-02",
            "nexus-serialization-encrypted-1" => "handler-b-key-01",
            "nexus-serialization-encrypted-2" => "handler-b-key-02",
            _ => throw new ArgumentException("Unknown endpoint", nameof(endpoint)),
        };

    private static void AssertMetadata(Payload payload, string endpoint)
    {
        Assert.Equal("binary/nexus-aes-gcm", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal(endpoint["nexus-serialization-".Length..], payload.Metadata["encryption-key-id"].ToStringUtf8());
        Assert.Equal(endpoint, payload.Metadata["nexus-endpoint-name"].ToStringUtf8());
    }

    private static Payload Decrypt(Payload payload, string key)
    {
        var data = payload.Data.ToByteArray();
        var plaintext = new byte[data.Length - 28];
        using var aes = new AesGcm(Encoding.UTF8.GetBytes(key), 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(12, plaintext.Length), data.AsSpan(data.Length - 16), plaintext);
        return Payload.Parser.ParseFrom(plaintext);
    }
}
