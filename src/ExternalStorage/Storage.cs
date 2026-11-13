namespace TemporalioSamples.ExternalStorage;

using Amazon.Runtime;
using Amazon.S3;
using Temporalio.Converters;
using Temporalio.Extensions.Aws.S3Driver;
using Temporalio.Extensions.Aws.S3Driver.AwsSdk;

/// <summary>
/// Every process in this sample must agree on the codec and storage configuration, so each can
/// read what the others wrote.
/// </summary>
public static class Storage
{
    /// <summary>Set to a bucket name to use real S3 instead of the local mock.</summary>
    public const string BucketEnvVar = "EXTERNAL_STORAGE_S3_BUCKET";

    public const int MockPort = 9100;
    public const string MockBucket = "temporal-payloads";
    public const string MockAccessKey = "test";
    public const string MockSecretKey = "test";

    private static readonly string? RealBucket =
        Environment.GetEnvironmentVariable(BucketEnvVar) is { Length: > 0 } bucket ? bucket : null;

    public static string Bucket => RealBucket ?? MockBucket;

    public static AmazonS3Client CreateS3Client() =>
        // Region and credentials come from the usual AWS configuration, such as environment
        // variables or ~/.aws.
        RealBucket != null ? new AmazonS3Client() : CreateMockS3Client(MockPort);

    // A client for the local mock S3 server started by S3Mock.
    public static AmazonS3Client CreateMockS3Client(int port) =>
        new(
            new BasicAWSCredentials(MockAccessKey, MockSecretKey),
            new AmazonS3Config
            {
                ServiceURL = $"http://localhost:{port}",
                AuthenticationRegion = "us-east-1",
                // Virtual-hosted addressing would need temporal-payloads.localhost to resolve,
                // which depends on how the OS handles *.localhost.
                ForcePathStyle = true,
            });

    public static DataConverter CreateDataConverter(IAmazonS3 s3Client, string bucket)
    {
        var driver = new S3StorageDriver(new AwsSdkS3StorageDriverClient(s3Client), bucket);
        return DataConverter.Default with
        {
            PayloadCodec = new ZlibCodec(),
            ExternalStorage = new(driver),
        };
    }
}
