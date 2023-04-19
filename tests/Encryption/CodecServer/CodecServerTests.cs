namespace TemporalioSamples.Tests.Encryption.CodecServer;

using Google.Protobuf;
using Microsoft.AspNetCore.Mvc.Testing;
using Temporalio.Api.Common.V1;
using TemporalioSamples.Encryption.CodecServer;
using Xunit;
using Xunit.Abstractions;

public class CodecServerTests : TestBase, IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public CodecServerTests(ITestOutputHelper output, WebApplicationFactory<Program> factory)
        : base(output) => this.factory = factory;

    [Fact]
    public async Task CodecServer_WithEncryptionCodec_WorksProperly()
    {
        using var client = factory.CreateClient();

        // Create unencrypted payload
        var origPayloads = new Payloads()
        {
            Payloads_ =
            {
                new Payload()
                {
                    Metadata = { ["meta-key"] = ByteString.CopyFromUtf8("meta-value") },
                    Data = ByteString.CopyFromUtf8("some-value"),
                },
            },
        };

        // Encode
        using var encContent = new StringContent(JsonFormatter.Default.Format(origPayloads), null, "application/json");
        using var encResp = await client.PostAsync("/encode", encContent);
        encResp.EnsureSuccessStatusCode();
        var encPayloads = JsonParser.Default.Parse<Payloads>(
            await encResp.Content.ReadAsStringAsync());
        // Check encoding and key ID
        var encPayload = encPayloads.Payloads_.Single();
        Assert.Equal("binary/encrypted", encPayload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal("test-key-id", encPayload.Metadata["encryption-key-id"].ToStringUtf8());

        // Decode
        using var decContent = new StringContent(JsonFormatter.Default.Format(encPayloads), null, "application/json");
        using var decResp = await client.PostAsync("/decode", decContent);
        decResp.EnsureSuccessStatusCode();
        var decPayloads = JsonParser.Default.Parse<Payloads>(
            await decResp.Content.ReadAsStringAsync());
        Assert.Equal(origPayloads, decPayloads);
    }
}