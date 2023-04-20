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

    [AesGcmSupportedFact]
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
        var encRespBody = await encResp.Content.ReadAsStringAsync();
        Assert.True(encResp.IsSuccessStatusCode, encRespBody);
        var encPayloads = JsonParser.Default.Parse<Payloads>(encRespBody);
        // Check encoding and key ID
        var encPayload = encPayloads.Payloads_.Single();
        Assert.Equal("binary/encrypted", encPayload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal("test-key-id", encPayload.Metadata["encryption-key-id"].ToStringUtf8());

        // Decode
        using var decContent = new StringContent(JsonFormatter.Default.Format(encPayloads), null, "application/json");
        using var decResp = await client.PostAsync("/decode", decContent);
        var decRespBody = await decResp.Content.ReadAsStringAsync();
        Assert.True(decResp.IsSuccessStatusCode, decRespBody);
        var decPayloads = JsonParser.Default.Parse<Payloads>(decRespBody);
        Assert.Equal(origPayloads, decPayloads);
    }

    public sealed class AesGcmSupportedFactAttribute : FactAttribute
    {
        public AesGcmSupportedFactAttribute()
        {
            if (!System.Security.Cryptography.AesGcm.IsSupported)
            {
                Skip = "AesGcm not supported on this platform";
            }
        }
    }
}