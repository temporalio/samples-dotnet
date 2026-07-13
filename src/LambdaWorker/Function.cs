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
            config =>
            {
                config.ApplyOpenTelemetryDefaults();
                LambdaWorkerSample.ConfigureWorkerOptions(config.WorkerOptions);
            });

    public Task HandlerAsync(Stream input, ILambdaContext context) =>
        WorkerHandler(input, context);
}
// @@@SNIPEND
