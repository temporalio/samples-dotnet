using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.WorkerSpecificTaskQueues;

public static class WorkerSpecificActivities
{
    [Activity]
#pragma warning disable CA1054
    public static async Task<string> DownloadFileToWorkerFileSystemAsync(string url)
#pragma warning restore CA1054
    {
        var path = Path.GetTempFileName();
        ActivityExecutionContext.Current.Logger.LogInformation("Downloading {Url} and saving to path {Path}", url, path);
        // Here's were the real download code goes.
        var body = Encoding.UTF8.GetBytes("downloaded body");
        await Task.Delay(TimeSpan.FromSeconds(3));
        await File.WriteAllBytesAsync(path, body);
        return path;
    }

    [Activity]
    public static async Task WorkOnFileInWorkerFileSystemAsync(string path)
    {
        var content = await File.ReadAllBytesAsync(path);
        var checksum = ComputeSha256Hash(content);
        await Task.Delay(TimeSpan.FromSeconds(3));
        ActivityExecutionContext.Current.Logger.LogInformation("Did some work on {Path}, checksum: {Checksum}", path, checksum);
    }

    [Activity]
    public static async Task CleanupFileFromWorkerFileSystemAsync(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        ActivityExecutionContext.Current.Logger.LogInformation("Removing {Path}", path);
        File.Delete(path);
    }

    private static string ComputeSha256Hash(byte[] input)
    {
        var hash = SHA256.HashData(input);
        var sb = new StringBuilder();
        foreach (var c in hash)
        {
            sb.Append(c.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}