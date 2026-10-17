using System.CommandLine;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.ClientMtls;

// Options shared by client commands
var targetHostOption = new Option<string>("--target-host")
{
    Description = "Host:port to connect to",
    Required = true,
};
var namespaceOption = new Option<string>("--namespace")
{
    Description = "Namespace to connect to",
    Required = true,
};
var clientCertOption = new Option<FileInfo>("--client-cert")
{
    Description = "Client certificate file for auth",
    Required = true,
};
var clientKeyOption = new Option<FileInfo>("--client-key")
{
    Description = "Client key file for auth",
    Required = true,
};

async Task<TemporalClient> ConnectClientAsync(ParseResult parseResult)
{
    return await TemporalClient.ConnectAsync(
        new(parseResult.GetRequiredValue(targetHostOption))
        {
            Namespace = parseResult.GetRequiredValue(namespaceOption),
            // Set TLS options with client certs. Note, more options could
            // be added here for server CA (i.e. "ServerRootCACert") or SNI
            // override (i.e. "Domain") for self-hosted environments with
            // self-signed certificates.
            Tls = new()
            {
                ClientCert =
                    await File.ReadAllBytesAsync(parseResult.GetRequiredValue(clientCertOption).FullName),
                ClientPrivateKey =
                    await File.ReadAllBytesAsync(parseResult.GetRequiredValue(clientKeyOption).FullName),
            },
        });
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync(parseResult);

    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "client-mtls-sample").
            AddWorkflow<GreetingWorkflow>());
    await worker.ExecuteAsync(cancellationToken);
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync(parseResult);

    var result = await client.ExecuteWorkflowAsync(
        (GreetingWorkflow wf) => wf.RunAsync("Temporal"),
        new(id: "client-mtls-workflow-id", taskQueue: "client-mtls-sample")
        {
            Rpc = new() { CancellationToken = cancellationToken },
        });
    Console.WriteLine("Workflow result: {0}", result);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var runWorkerCommand = new Command("run-worker", "Run worker")
{
    targetHostOption,
    namespaceOption,
    clientCertOption,
    clientKeyOption,
};
runWorkerCommand.SetAction(RunWorkerAsync);

var executeWorkflowCommand = new Command("execute-workflow", "Execute workflow")
{
    targetHostOption,
    namespaceOption,
    clientCertOption,
    clientKeyOption,
};
executeWorkflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Client mTLS sample")
{
    GenClientCerts.CreateCommand(),
    runWorkerCommand,
    executeWorkflowCommand,
};
return await rootCommand.Parse(args).InvokeAsync();
