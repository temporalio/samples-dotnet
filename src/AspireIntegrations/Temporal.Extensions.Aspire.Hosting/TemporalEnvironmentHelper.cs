namespace Temporal.Extensions.Aspire.Hosting;

internal static class TemporalEnvironmentHelper
{
    internal static void AddEnvironmentVariables(
        EnvironmentCallbackContext ctx,
        TemporalResourceOptions options,
        object temporalAddress,
        object temporalUiAddress)
    {
        ctx.EnvironmentVariables["TEMPORAL_ADDRESS"] = temporalAddress;
        ctx.EnvironmentVariables["TEMPORAL_UI_ADDRESS"] = temporalUiAddress;
        ctx.EnvironmentVariables["TEMPORAL_NAMESPACE"] = options.Namespace;
        ctx.EnvironmentVariables["TEMPORAL_DEFAULT_NAMESPACE"] = options.Namespace;

        if (!string.IsNullOrEmpty(options.CodecAuth))
            ctx.EnvironmentVariables["TEMPORAL_CODEC_AUTH"] = options.CodecAuth;

        if (!string.IsNullOrEmpty(options.CodecEndpoint))
            ctx.EnvironmentVariables["TEMPORAL_CODEC_ENDPOINT"] = options.CodecEndpoint;
    }
}
