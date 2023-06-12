using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.ActivityStickyQueues;

public class StickyActivities
{
    [Activity]
    public async Task DownloadFileToWorkerFileSystemAsync(string uri, string path)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Downloading {url} and saving to path {path}", uri, path);
        // Here's were the real download code goes.
        var body = Encoding.UTF8.GetBytes("downloaded body");
        await Task.Delay(TimeSpan.FromSeconds(3));
        await File.WriteAllBytesAsync(path, body);
    }

    [Activity]
    public async Task WorkOnFileInWorkerFileSystemAsync(string path)
    {
        var content = await File.ReadAllBytesAsync(path);
        var checksum = ComputeMd5Hash(content);
        await Task.Delay(TimeSpan.FromSeconds(3));
        ActivityExecutionContext.Current.Logger.LogInformation("Did some work on {path}, checksum: {checksum}", path, checksum);
    }

    [Activity]
    public async Task CleanupFileFromWorkerFileSystemAsync(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        ActivityExecutionContext.Current.Logger.LogInformation("Removing {path}", path);
        File.Delete(path);
    }

    private static string ComputeMd5Hash(byte[] input)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(input);
        var sb = new StringBuilder();
        foreach (var c in hash)
        {
            sb.Append(c.ToString("x2"));
        }

        return sb.ToString();
    }
}