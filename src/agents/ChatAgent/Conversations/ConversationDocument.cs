namespace ChatAgent.Conversations;

// Depo dokümanları — aggregate DEĞİL: iş kuralı/invariant taşımaz (plan Complexity #1).
public sealed class ConversationDocument
{
    public string Id { get; set; } = default!;              // "conv_..." — Marten identity
    public string AgentName { get; set; } = default!;       // "public" / "assistant"
    public string? OwnerUserId { get; set; }                 // login: token'daki sub; anonim: null
    public string Title { get; set; } = ConversationRules.DefaultTitle;
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset LastActivityTime { get; set; }     // liste sırası + anonim TTL bunu okur
}

// Item'lar immutable'dır: eklenir, güncellenmez; conversation silinince toplu silinir.
public sealed class ConversationItemDocument
{
    public string Id { get; set; } = default!;               // "{conversationId}:{itemId}" — global benzersiz
    public string ConversationId { get; set; } = default!;
    public string ItemId { get; set; } = default!;           // framework'ün item id'si (msg_...)
    public long Sequence { get; set; }                       // konuşma içi monoton sıra
    public string ItemJson { get; set; } = default!;         // ItemResource, System.Text.Json ile (polimorfik)
    public DateTimeOffset CreatedTime { get; set; }
}