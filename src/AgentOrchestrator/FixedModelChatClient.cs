using Microsoft.Extensions.AI;

namespace AgentOrchestrator;

// Responses istegindeki "model" alanini proxy agent adiyla (public/assistant) dolduruyor;
// bu deger dogrudan OpenAI'a model olarak gidip "model_not_found" hatasi veriyordu.
// Agent zaten URL path'i ile secildigi icin LLM modelini ORCHESTRATOR sahiplenir: gelen
// ModelId ne olursa olsun configdeki modeli (OpenAI:Model) zorlar.
public sealed class FixedModelChatClient(IChatClient innerClient, string modelId)
    : DelegatingChatClient(innerClient)
{
    private ChatOptions ForceModel(ChatOptions? options)
    {
        options = options?.Clone() ?? new ChatOptions();
        options.ModelId = modelId;
        return options;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetResponseAsync(messages, ForceModel(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(messages, ForceModel(options), cancellationToken);
}