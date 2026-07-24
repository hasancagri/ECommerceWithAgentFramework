using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ChatAgent.Conversations;

// Konuşma deposu (R2-pivot): MAF'ın storage arayüzleri internal çıktığı için framework'e takılmayan
// kendi Marten servisimiz. Mesajlar ChatMessage olarak STJ+AIJsonUtilities ile saklanır.
public sealed class ConversationStore(IDocumentStore store, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions Json = AIJsonUtilities.DefaultOptions;

    private readonly int _contextWindowItems = Math.Max(1,
        configuration.GetValue("Chat:ContextWindowItems", ConversationRules.DefaultContextWindowItems));

    public async Task<ConversationDocument> CreateAsync(string agentName, string? ownerUserId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var doc = new ConversationDocument
        {
            Id = $"conv_{Guid.NewGuid():N}",
            AgentName = agentName,
            OwnerUserId = ownerUserId,
            CreatedTime = now,
            LastActivityTime = now,
        };

        await using var session = store.LightweightSession();
        session.Store(doc);
        await session.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<ConversationDocument?> GetAsync(string conversationId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        return await session.LoadAsync<ConversationDocument>(conversationId, ct);
    }

    public async Task<(IReadOnlyList<ConversationDocument> Items, int TotalCount)> ListOwnedAsync(
        string ownerUserId, int page, int pageSize, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var query = session.Query<ConversationDocument>().Where(c => c.OwnerUserId == ownerUserId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.LastActivityTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<ConversationItemDocument> Items, int TotalCount)> ListItemsAsync(
        string conversationId, int page, int pageSize, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var query = session.Query<ConversationItemDocument>().Where(i => i.ConversationId == conversationId);
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(i => i.Sequence)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }

    // Modele giden pencere (FR-005): kronolojik geçmişin SON N öğesi. Depo tam kalır; UI tam okur.
    public async Task<List<ChatMessage>> LoadContextWindowAsync(string conversationId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var chronological = await session.Query<ConversationItemDocument>()
            .Where(i => i.ConversationId == conversationId)
            .OrderBy(i => i.Sequence)
            .ToListAsync(ct);

        return ConversationRules
            .TakeContextWindow((IReadOnlyList<ConversationItemDocument>)chronological, _contextWindowItems)
            .Select(Deserialize)
            .ToList();
    }

    // Turn sonu kalıcılaştırma: kullanıcı mesajı + koşunun ürettiği tüm mesajlar (araç çağrıları dahil,
    // FR-006). Başlık ilk kullanıcı mesajından bir kez türetilir; LastActivity liste/TTL için güncellenir.
    public async Task AppendMessagesAsync(string conversationId, IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        await using var session = store.LightweightSession();
        var doc = await session.LoadAsync<ConversationDocument>(conversationId, ct)
                  ?? throw new InvalidOperationException($"Conversation '{conversationId}' bulunamadı.");

        var nextSequence = 1 + (await session.Query<ConversationItemDocument>()
            .Where(i => i.ConversationId == conversationId)
            .MaxAsync(i => (long?)i.Sequence, ct) ?? 0);

        var now = DateTimeOffset.UtcNow;
        foreach (var message in messages)
        {
            session.Store(new ConversationItemDocument
            {
                Id = $"{conversationId}:{nextSequence}",
                ConversationId = conversationId,
                ItemId = message.MessageId ?? $"msg_{nextSequence}",
                Sequence = nextSequence,
                ItemJson = JsonSerializer.Serialize(message, Json),
                CreatedTime = now,
            });
            nextSequence++;

            if (doc.Title == ConversationRules.DefaultTitle && message.Role == ChatRole.User)
                doc.Title = ConversationRules.DeriveTitle(message.Text);
        }

        doc.LastActivityTime = now;
        session.Store(doc);
        await session.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteExpiredAnonymousAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var session = store.LightweightSession();
        var expiredIds = await session.Query<ConversationDocument>()
            .Where(c => c.OwnerUserId == null && c.LastActivityTime < cutoff)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (expiredIds.Count == 0)
            return 0;

        session.DeleteWhere<ConversationItemDocument>(i => expiredIds.Contains(i.ConversationId));
        session.DeleteWhere<ConversationDocument>(c => expiredIds.Contains(c.Id));
        await session.SaveChangesAsync(ct);
        return expiredIds.Count;
    }

    public static ChatMessage Deserialize(ConversationItemDocument doc)
        => JsonSerializer.Deserialize<ChatMessage>(doc.ItemJson, Json)
           ?? throw new InvalidOperationException($"Item '{doc.Id}' çözülemedi.");
}