using System.Security.Claims;
using Microsoft.Extensions.AI;

namespace ChatAgent.Conversations;

// Konuşma uçları (contracts/chat-conversations-api.md). Sahiplik BURADA zorlanır: kimlik token'daki
// sub'dan çözülür; başkasının konuşması "bulunamadı" gibi davranır (FR-007, varlık sızdırılmaz).
public static class MyConversationsEndpoints
{
    public record CreateConversationRequest(string AgentName);

    private static readonly string[] KnownAgents = ["public", "assistant"];

    public static void MapMyConversationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/my-conversations").WithTags("Conversations");

        // Anonim de sohbet açar (US3); login'de sahip token'dan yazılır — istemci beyanına güvenilmez (R5).
        group.MapPost("", async (CreateConversationRequest body, ClaimsPrincipal user,
            ConversationStore store, CancellationToken ct) =>
        {
            if (!KnownAgents.Contains(body.AgentName))
                return Results.BadRequest(new { error = "UNKNOWN_AGENT" });

            var doc = await store.CreateAsync(body.AgentName, user.FindFirst("sub")?.Value, ct);
            return Results.Ok(new { conversationId = doc.Id });
        }).AllowAnonymous();

        group.MapGet("", async (ClaimsPrincipal user, ConversationStore store,
            int page, int pageSize, CancellationToken ct) =>
        {
            var sub = user.FindFirst("sub")!.Value;
            page = Math.Max(1, page);
            pageSize = pageSize == 0 ? 20 : Math.Clamp(pageSize, 1, 100);

            var (items, totalCount) = await store.ListOwnedAsync(sub, page, pageSize, ct);
            return Results.Ok(new
            {
                items = items.Select(c => new
                {
                    conversationId = c.Id, title = c.Title,
                    agentName = c.AgentName, lastActivityTime = c.LastActivityTime,
                }),
                page, pageSize, totalCount,
            });
        }).RequireAuthorization();

        // TAM geçmiş (FR-004): pencere bu yola uygulanmaz; kırpma yalnız model input'undadır (R3).
        group.MapGet("/{id}/items", async (string id, ClaimsPrincipal user, ConversationStore store,
            int page, int pageSize, CancellationToken ct) =>
        {
            var sub = user.FindFirst("sub")!.Value;
            page = Math.Max(1, page);
            pageSize = pageSize == 0 ? 100 : Math.Clamp(pageSize, 1, 500);

            var doc = await store.GetAsync(id, ct);
            if (doc is null || doc.OwnerUserId != sub)
                return Results.NotFound();

            var (items, totalCount) = await store.ListItemsAsync(id, page, pageSize, ct);
            return Results.Ok(new
            {
                items = items.Select(i =>
                {
                    var view = ConversationItemViews.ToView(i);
                    return new
                    {
                        sequence = i.Sequence, role = view.Role, text = view.Text,
                        kind = view.Kind, createdTime = i.CreatedTime,
                    };
                }),
                page, pageSize, totalCount, title = doc.Title,
            });
        }).RequireAuthorization();
    }
}

// ChatMessage → UI görünümü: metinli mesajlar rol+metin, araç çağrısı/sonucu 'tool' olarak sadeleşir.
public static class ConversationItemViews
{
    public record ItemView(string Kind, string? Role, string? Text);

    public static ItemView ToView(ConversationItemDocument doc)
    {
        var message = ConversationStore.Deserialize(doc);

        var toolNames = message.Contents
            .Select(c => c switch
            {
                FunctionCallContent call => call.Name,
                FunctionResultContent => "result",
                _ => null,
            })
            .Where(n => n is not null)
            .ToList();

        return toolNames.Count > 0
            ? new ItemView("tool", message.Role.Value, string.Join(", ", toolNames))
            : new ItemView("message", message.Role.Value, message.Text);
    }
}