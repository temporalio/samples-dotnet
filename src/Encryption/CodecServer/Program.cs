namespace TemporalioSamples.Encryption.CodecServer;

using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using TemporalioSamples.Encryption.Codec;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Setup console logging, codec, and cors
        builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Information);
        builder.Services.AddSingleton<IPayloadCodec>(ctx => new EncryptionCodec());
        builder.Services.AddCors();

        var app = builder.Build();

        // We need CORS so that the browser can access this endpoint from a
        // different origin
        app.UseCors(
            builder => builder.
                WithHeaders("content-type", "x-namespace").
                WithMethods("POST").
                // This list may need to be customized based on where the UI
                // is communicating from
                WithOrigins("http://localhost:8080", "http://localhost:8233", "https://cloud.temporal.io"));

        // These are the endpoints called for encrypt/decrypt
        app.MapPost("/encode", EncodeAsync);
        app.MapPost("/decode", DecodeAsync);

        app.Run();
    }

    private static Task<IResult> EncodeAsync(
        HttpContext ctx, IPayloadCodec codec) => ApplyCodecFuncAsync(ctx, codec.EncodeAsync);

    private static Task<IResult> DecodeAsync(
        HttpContext ctx, IPayloadCodec codec) => ApplyCodecFuncAsync(ctx, codec.DecodeAsync);

    private static async Task<IResult> ApplyCodecFuncAsync(
        HttpContext ctx, Func<IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> func)
    {
        // Read payloads as JSON
        if (ctx.Request.ContentType?.StartsWith("application/json") != true)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        Payloads inPayloads;
        using (var reader = new StreamReader(ctx.Request.Body))
        {
            inPayloads = JsonParser.Default.Parse<Payloads>(await reader.ReadToEndAsync());
        }

        // Apply codec func
        var outPayloads = new Payloads() { Payloads_ = { await func(inPayloads.Payloads_) } };

        // Return JSON
        return Results.Text(JsonFormatter.Default.Format(outPayloads), "application/json");
    }
}