namespace TemporalioSamples.NexusDependencyInjection.Handler;

using NexusRpc.Handlers;

[NexusServiceHandler(typeof(IGreetingService))]
public class GreetingServiceHandler
{
    private readonly IGreetingClient greetingClient;

    // The dependency is injected by the container. The service is registered with
    // AddScopedNexusService in Program.cs, so a scoped instance is created for each operation.
    public GreetingServiceHandler(IGreetingClient greetingClient) => this.greetingClient = greetingClient;

    [NexusOperationHandler]
    public IOperationHandler<IGreetingService.SayHelloInput, string> SayHello() =>
        // A simple sync handler that uses the injected dependency
        OperationHandler.Sync<IGreetingService.SayHelloInput, string>(
            (ctx, input) => greetingClient.GetGreetingAsync(input.Name));
}
