using System.Security.Claims;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ChatAgent.Conversations;

// Widget'ın sohbet ucu (R1-pivot): geçmiş Marten'dan pencereyle yüklenir, session'a verilir,
// koşu SSE ile akar, sonunda kullanıcı+asistan mesajları (araç çağrıları dahil) kalıcılaşır.
// Depo yoksa istek düpedüz hata verir — sessiz in-memory düşüş yok (FR-011).
public static class ChatStreamEndpoint
{
    public record ChatRequest(string ConversationId, string Message);

    public static void MapChatStreamEndpoint(this WebApplication app)
    {
        app.MapPost("/v1/chat", async (ChatRequest body, ClaimsPrincipal user, HttpContext http,
            ConversationStore conversations, IServiceProvider services, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Message))
                return Results.BadRequest(new { error = "EMPTY_MESSAGE" });

            var doc = await conversations.GetAsync(body.ConversationId, ct);

            // Sahiplik (FR-007): login konuşması yalnız sahibine; anonim (sahipsiz) konuşma id bilene.
            // Yok ya da başkasının → 404; BFF yeni konuşma açar (FR-010). Varlık sızdırılmaz.
            var sub = user.FindFirst("sub")?.Value;
            if (doc is null || (doc.OwnerUserId is not null && doc.OwnerUserId != sub))
                return Results.NotFound();

            var agent = services.GetRequiredKeyedService<AIAgent>(doc.AgentName);

            var history = await conversations.LoadContextWindowAsync(doc.Id, ct);
            var session = await agent.CreateSessionAsync(ct);
            session.SetInMemoryChatHistory(history);

            var userMessage = new ChatMessage(ChatRole.User, body.Message);

            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            var updates = new List<AgentResponseUpdate>();
            await foreach (var update in agent.RunStreamingAsync(userMessage, session, null, ct))
            {
                updates.Add(update);
                if (!string.IsNullOrEmpty(update.Text))
                    await WriteEventAsync(http.Response, new { delta = update.Text }, ct);
            }

            // Önce kalıcılaştır sonra done — istemci done gördüyse geçmiş DB'dedir.
            var response = updates.ToAgentResponse();
            var produced = new List<ChatMessage> { userMessage };
            produced.AddRange(response.Messages);
            await conversations.AppendMessagesAsync(doc.Id, produced, CancellationToken.None);

            await WriteEventAsync(http.Response, new { done = true, conversationId = doc.Id }, ct);
            return Results.Empty;
        }).AllowAnonymous();
    }

    private static async Task WriteEventAsync(HttpResponse response, object payload, CancellationToken ct)
    {
        await response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}