// @@@SNIPSTART dotnet-lambda-worker
namespace TemporalioSamples.LambdaWorker;

using Amazon.Lambda.Core;
using Temporalio.Common;
using Temporalio.Extensions.Aws.Lambda;
using Temporalio.Extensions.Aws.Lambda.OpenTelemetry;

public class LambdaFunction
{
    private static readonly Func<object?, ILambdaContext, Task> WorkerHandler =
        TemporalLambdaWorker.CreateHandler(
            new WorkerDeploymentVersion(
                LambdaWorkerSample.DeploymentName,
                LambdaWorkerSample.BuildId),
            Configure);

    public Task HandlerAsync(Stream input, ILambdaContext context) =>
        WorkerHandler(input, context);

    private static void Configure(LambdaWorkerConfig config)
    {
        LambdaWorkerSample.ConfigureWorkerOptions(config.WorkerOptions);
        LambdaWorkerOpenTelemetry.ApplyDefaults(config);
    }
}
// @@@SNIPEND
