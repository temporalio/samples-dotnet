namespace Temporal.Extensions.Aspire.Hosting;

internal static class TemporalArgsBuilder
{
    /// <summary>
    /// Builds the CLI arguments for a Temporal dev server.
    /// When <paramref name="fixedIpAndPort"/> is true the IP is always "0.0.0.0" and the port
    /// is always <see cref="TemporalResourceConstants.DefaultServiceEndpointPort"/> (container mode).
    /// When false, IP and port are taken from <paramref name="options"/> and --ui-port is also emitted (CLI mode).
    /// </summary>
    internal static string[] BuildArgs(TemporalResourceOptions options, bool fixedIpAndPort = false)
    {
        var args = new List<string> { "server", "start-dev" };

        if (fixedIpAndPort)
        {
            args.AddRange(["--ip", "0.0.0.0"]);
            args.AddRange(["--port", $"{TemporalResourceConstants.DefaultServiceEndpointPort}"]);
        }
        else
        {
            args.AddRange(["--ip", options.Ip]);
            args.AddRange(["--port", options.Port.ToString()]);
            args.AddRange(["--ui-port", options.UIPort.ToString()]);
        }

        if (options.IsHeadless)
            args.Add("--headless");

        args.AddRange(["--log-level", options.DevServerOptions.LogLevel]);
        args.AddRange(["--log-format", options.DevServerOptions.LogFormat]);

        foreach (var ns in options.AdditionalNamespaces)
            args.AddRange(["--namespace", ns]);

        if (options.SearchAttributes != null)
        {
            foreach (var sa in options.SearchAttributes)
                args.AddRange(["--search-attribute", $"{sa.Name}={sa.ValueType}"]);
        }

        foreach (var dv in options.DynamicConfigValues)
            args.AddRange(["--dynamic-config-value", dv]);

        if (!string.IsNullOrEmpty(options.CodecAuth))
            args.AddRange(["--codec-auth", options.CodecAuth]);

        if (!string.IsNullOrEmpty(options.CodecEndpoint))
            args.AddRange(["--codec-endpoint", options.CodecEndpoint]);

        if (!string.IsNullOrEmpty(options.ApiKey))
            args.AddRange(["--api-key", options.ApiKey]);

        return args.ToArray();
    }
}
