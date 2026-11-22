namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

// Streams the inner client's first update, then holds the rest until the test releases them, so a
// test can watch the stream while the model call is still open, however slow the machine is.
public sealed class FirstUpdateGateChatClient(IChatClient innerClient, Task release) : DelegatingChatClient(innerClient)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var first = true;
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
            if (first)
            {
                first = false;
                await release.WaitAsync(cancellationToken);
            }
        }
    }
}
