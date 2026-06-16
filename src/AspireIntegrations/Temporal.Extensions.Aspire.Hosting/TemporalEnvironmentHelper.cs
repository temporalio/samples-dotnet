namespace Temporal.Extensions.Aspire.Hosting;

internal static class TemporalEnvironmentHelper
{
    internal static void AddCodecEnvironmentVariables(
        EnvironmentCallbackContext ctx,
        TemporalResourceOptions options)
    {
        if (!string.IsNullOrEmpty(options.CodecAuth))
            ctx.EnvironmentVariables["TEMPORAL_CODEC_AUTH"] = options.CodecAuth;

        if (!string.IsNullOrEmpty(options.CodecEndpoint))
            ctx.EnvironmentVariables["TEMPORAL_CODEC_ENDPOINT"] = options.CodecEndpoint;
    }
}
