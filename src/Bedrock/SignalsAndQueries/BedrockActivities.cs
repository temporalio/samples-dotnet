using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Temporalio.Activities;

namespace TemporalioSamples.Bedrock.SignalsAndQueries;

public record PromptBedrockActivityArgs(string Prompt);
public record PromptBedrockActivityResult(string Response);

public interface IBedrockActivities
{
    [Activity]
    Task<PromptBedrockActivityResult> PromptBedrockAsync(PromptBedrockActivityArgs args);
}

public class BedrockActivities(IAmazonBedrockRuntime bedrock) : IBedrockActivities
{
    [Activity]
    public async Task<PromptBedrockActivityResult> PromptBedrockAsync(PromptBedrockActivityArgs args)
    {
        var body = JsonSerializer.Serialize(new
        {
            prompt = args.Prompt,
            max_gen_len = 512,
            temperature = 0.1,
            top_p = 0.2,
        });

        var request = new InvokeModelRequest
        {
            ModelId = "meta.llama3-1-70b-instruct-v1:0",
            Accept = "application/json",
            ContentType = "application/json",
            Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)),
        };

        var response = await bedrock.InvokeModelAsync(request);
        var modelResponse = await JsonNode.ParseAsync(response.Body);
        var responseText = modelResponse?["generation"]?.ToString() ?? string.Empty;
        return new(responseText);
    }
}