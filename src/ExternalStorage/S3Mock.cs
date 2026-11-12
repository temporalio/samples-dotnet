namespace TemporalioSamples.ExternalStorage;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using S3ServerLibrary;
using S3ServerLibrary.S3Objects;
using WatsonWebserver.Core;

/// <summary>
/// A local S3 server that keeps objects in memory until it stops. It serves only the requests the
/// S3 driver makes.
/// </summary>
public static class S3Mock
{
    private static readonly Owner MockOwner = new("test", "test");

    public static async Task RunAsync(CancellationToken cancellationToken, int port = Storage.MockPort)
    {
        var objects = new ConcurrentDictionary<string, byte[]>();
        var settings = new S3ServerSettings
        {
            EnableSignatures = true,
            Webserver = new WebserverSettings("localhost", port, false),
        };
        settings.Telemetry.Enable = false;
        using var server = new S3Server(settings);

        server.Service.GetSecretKey = ctx => ctx.Request.AccessKey == Storage.MockAccessKey ?
            Storage.MockSecretKey : throw new S3Exception(new Error(ErrorCode.InvalidAccessKeyId));

        byte[] Get(S3Context ctx)
        {
            if (ctx.Request.Bucket != Storage.MockBucket)
            {
                throw new S3Exception(new Error(ErrorCode.NoSuchBucket));
            }
            return objects.TryGetValue(ctx.Request.Key, out var data) ?
                data : throw new S3Exception(new Error(ErrorCode.NoSuchKey));
        }

        server.Object.Exists = async ctx =>
        {
            var data = Log(ctx, () => Get(ctx));
            return new ObjectMetadata(
                ctx.Request.Key, DateTime.UtcNow, ETag(data), data.Length, MockOwner, StorageClassEnum.STANDARD);
        };
        server.Object.Read = async ctx =>
        {
            var data = Log(ctx, () => Get(ctx));
            return new S3Object(
                ctx.Request.Key, "1", true, DateTime.UtcNow, ETag(data), data.Length, MockOwner, data, "application/octet-stream", StorageClassEnum.STANDARD);
        };
        server.Object.Write = async ctx =>
        {
            var data = await ReadBodyAsync(ctx);
            Log(ctx, () => objects[ctx.Request.Key] = data);
        };

        server.Start();
        Console.WriteLine($"Mock S3 listening on http://localhost:{port}, bucket {Storage.MockBucket}");
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        server.Stop();
    }

    private static T Log<T>(S3Context ctx, Func<T> handle)
    {
        var line = $"{ctx.Http.Request.Method} /{ctx.Request.Bucket}/{ctx.Request.Key}";
        try
        {
            var result = handle();
            Console.WriteLine($"{line} status=200");
            return result;
        }
        catch (S3Exception e)
        {
            Console.WriteLine($"{line} status={e.Error.Code}");
            throw;
        }
    }

    // The AWS SDK uploads with aws-chunked encoding, which must be reassembled.
    private static async Task<byte[]> ReadBodyAsync(S3Context ctx)
    {
        if (!ctx.Request.Chunked)
        {
            return ctx.Request.DataAsBytes;
        }
        using var body = new MemoryStream();
        while (true)
        {
            var chunk = await ctx.Request.ReadChunk();
            if (chunk.Length > 0)
            {
                await body.WriteAsync(chunk.Data);
            }
            if (chunk.IsFinal)
            {
                return body.ToArray();
            }
        }
    }

    // The AWS SDK parses ETags as hex MD5 digests.
#pragma warning disable CA5351 // Not used for security
    private static string ETag(byte[] data) => Convert.ToHexStringLower(MD5.HashData(data));
#pragma warning restore CA5351
}
