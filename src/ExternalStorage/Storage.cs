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
    public const int S3Port = 5000;
    public const string S3Bucket = "temporal-payloads";
    public const string S3AccessKey = "test";
    public const string S3SecretKey = "test";

    public static AmazonS3Client CreateS3Client(int port = S3Port) =>
        new(
            new BasicAWSCredentials(S3AccessKey, S3SecretKey),
            new AmazonS3Config
            {
                ServiceURL = $"http://localhost:{port}",
                AuthenticationRegion = "us-east-1",
                // Virtual-hosted addressing would need temporal-payloads.localhost to resolve,
                // which depends on how the OS handles *.localhost.
                ForcePathStyle = true,
            });

    public static DataConverter CreateDataConverter(IAmazonS3 s3Client)
    {
        var driver = new S3StorageDriver(new AwsSdkS3StorageDriverClient(s3Client), S3Bucket);
        return DataConverter.Default with
        {
            PayloadCodec = new ZlibCodec(),
            ExternalStorage = new(driver),
        };
    }
}
