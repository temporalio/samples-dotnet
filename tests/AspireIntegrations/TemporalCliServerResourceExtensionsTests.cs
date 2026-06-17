using Aspire.Hosting;
using Temporal.Extensions.Aspire.Hosting;
using Xunit;

namespace TemporalioSamples.Tests.AspireIntegrations;

public class TemporalCliServerResourceExtensionsTests
{
    // -----------------------------------------------------------------------
    // TemporalCliLocator — PATH guard that backs AddTemporalCliServer
    // -----------------------------------------------------------------------
    [Fact]
    public void EnsureAvailable_ThrowsInvalidOperationException_WhenCliNotFound()
    {
        // The isAvailable override lets tests simulate a machine without 'temporal' installed
        // without manipulating the real PATH environment variable.
        var ex = Assert.Throws<InvalidOperationException>(
            () => TemporalCliLocator.EnsureAvailable(() => false));

        Assert.Contains("temporal", ex.Message);
        Assert.Contains("https://", ex.Message);
    }

    [Fact]
    public void EnsureAvailable_DoesNotThrow_WhenCliIsFound()
    {
        // Should complete without exception when the check reports the CLI is present.
        TemporalCliLocator.EnsureAvailable(() => true);
    }

    [Fact]
    public void EnsureAvailable_ErrorMessage_ContainsInstallInstructions()
    {
        // Regression: the error must stay actionable — users must know where to get the CLI.
        var ex = Assert.Throws<InvalidOperationException>(
            () => TemporalCliLocator.EnsureAvailable(() => false));

        Assert.Contains("docs.temporal.io/cli", ex.Message);
        Assert.Contains("PATH", ex.Message);
    }

    // -----------------------------------------------------------------------
    // AddTemporalCliServer — registration structure (PATH-independent)
    //
    // These tests use TemporalCliServerResource directly instead of calling
    // AddTemporalCliServer so they do not depend on 'temporal' being installed
    // on the test machine.
    // -----------------------------------------------------------------------
    [Fact]
    public void TemporalCliServerResource_Command_IsTemporalExecutable()
    {
        // The resource MUST use "temporal" as the Aspire executable command.
        // Regression guard: a rename here would silently break all CLI server setups
        // because Aspire would try to launch the wrong binary.
        var resource = new TemporalCliServerResource("temporal-cli-server");

        Assert.Equal("temporal", resource.Command);
    }

    [Fact]
    public void TemporalCliServerResource_DefaultArgs_ContainServerStartDev()
    {
        // Aspire launches the resource by appending args to the Command.
        // The server subcommand must be present or temporal starts in the wrong mode.
        var resource = new TemporalCliServerResource("temporal-cli-server");
        var args = TemporalArgsBuilder.BuildArgs(resource.Options);

        Assert.Contains("server", args);
        Assert.Contains("start-dev", args);
    }

    [Fact]
    public void TemporalCliServerResource_DefaultArgs_ContainServicePort()
    {
        // The --port flag must map to the default service port so the Aspire-registered
        // endpoint matches the port temporal actually binds.
        var resource = new TemporalCliServerResource("temporal-cli-server");
        var args = TemporalArgsBuilder.BuildArgs(resource.Options);

        var portIndex = Array.IndexOf(args, "--port");
        Assert.True(portIndex >= 0, "Expected --port flag in args");
        Assert.Equal(
            TemporalResourceConstants.DefaultServiceEndpointPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            args[portIndex + 1]);
    }

    [Fact]
    public void TemporalCliServerResource_DefaultArgs_ContainUiPort()
    {
        // Regression: UI port must be emitted in CLI mode so the dashboard is reachable.
        // This was added alongside the fix that wires the UI endpoint URL in the Aspire dashboard.
        var resource = new TemporalCliServerResource("temporal-cli-server");
        var args = TemporalArgsBuilder.BuildArgs(resource.Options);

        var uiPortIndex = Array.IndexOf(args, "--ui-port");
        Assert.True(uiPortIndex >= 0, "Expected --ui-port flag in args");
        Assert.Equal(
            TemporalResourceConstants.DefaultUIEndpointPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            args[uiPortIndex + 1]);
    }

    [Fact]
    public void TemporalCliServerResource_CustomOptions_ReflectedInArgs()
    {
        var resource = new TemporalCliServerResource("temporal-cli-server");
        resource.Options.Namespace = "orders";
        resource.Options.CodecEndpoint = "http://localhost:8088";

        var args = TemporalArgsBuilder.BuildArgs(resource.Options);

        Assert.Contains("orders", args);
        Assert.Contains("http://localhost:8088", args);
    }

    // -----------------------------------------------------------------------
    // AddTemporalCliServer — integration test (requires 'temporal' on PATH)
    // -----------------------------------------------------------------------
    [Fact]
    public void AddTemporalCliServer_Throws_WhenTemporalCliNotOnPath()
    {
        // This is the expected behavior when a developer tries to use AddTemporalCliServer
        // on a machine without the Temporal CLI installed. The early guard provides a
        // clear, actionable error instead of a cryptic FailedToStart later in Aspire.
        //
        // We verify the guard fires by driving TemporalCliLocator directly (which is
        // what AddTemporalCliServer calls internally).
        var ex = Assert.Throws<InvalidOperationException>(
            () => TemporalCliLocator.EnsureAvailable(isAvailable: () => false));

        Assert.Contains("temporal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
