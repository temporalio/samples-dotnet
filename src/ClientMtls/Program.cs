using System.CommandLine;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.ClientMtls;

var rootCommand = new RootCommand("Client mTLS sample");
rootCommand.AddCommand(GenClientCerts.CreateCommand());

// Helper for client commands
void AddClientCommand(
    string name,
    string desc,
    Func<ITemporalClient, CancellationToken, Task> func)
{
    var cmd = new Command(name, desc);
    rootCommand!.AddCommand(cmd);

    // Add options
    var targetHostOption = new Option<string>("--target-host", "Host:port to connect to");
    targetHostOption.IsRequired = true;
    var namespaceOption = new Option<string>("--namespace", "Namespace to connect to");
    namespaceOption.IsRequired = true;
    var clientCertOption = new Option<FileInfo>("--client-cert", "Client certificate file for auth");
    clientCertOption.IsRequired = true;
    var clientKeyOption = new Option<FileInfo>("--client-key", "Client key file for auth");
    clientKeyOption.IsRequired = true;
    cmd.AddOption(targetHostOption);
    cmd.AddOption(namespaceOption);
    cmd.AddOption(clientCertOption);
    cmd.AddOption(clientKeyOption);

    // Set handler
    cmd.SetHandler(async ctx =>
    {
        // Create client
        var client = await TemporalClient.ConnectAsync(
            new(ctx.ParseResult.GetValueForOption(targetHostOption)!)
            {
                Namespace = ctx.ParseResult.GetValueForOption(namespaceOption)!,
                // Set TLS options with client certs. Note, more options could
                // be added here for server CA (i.e. "ServerRootCACert") or SNI
                // override (i.e. "Domain") for self-hosted environments with
                // self-signed certificates.
                Tls = new()
                {
                    ClientCert =
                        await File.ReadAllBytesAsync(ctx.ParseResult.GetValueForOption(clientCertOption)!.FullName),
                    ClientPrivateKey =
                        await File.ReadAllBytesAsync(ctx.ParseResult.GetValueForOption(clientKeyOption)!.FullName),
                },
            });
        // Run
        await func(client, ctx.GetCancellationToken());
    });
}

// Command to run worker
AddClientCommand("run-worker", "Run worker", async (client, cancelToken) =>
{
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "client-mtls-sample").
            AddWorkflow<GreetingWorkflow>());
    await worker.ExecuteAsync(cancelToken);
});

// Command to run workflow
AddClientCommand("execute-workflow", "Execute workflow", async (client, cancelToken) =>
{
    var result = await client.ExecuteWorkflowAsync(
        (GreetingWorkflow wf) => wf.RunAsync("Temporal"),
        new(id: "client-mtls-workflow-id", taskQueue: "client-mtls-sample")
        {
            Rpc = new() { CancellationToken = cancelToken },
        });
    Console.WriteLine("Workflow result: {0}", result);
});

// Run
await rootCommand.InvokeAsync(args);