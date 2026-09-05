namespace WebApp.Chat;

// Tarayicidaki chat sayfasi ile ChatAgent arasindaki BFF proxy.
// Token HttpOnly cookie'de oldugundan tarayici orchestrator'a dogrudan erisemez;
// burada auth durumuna gore agent + token secilir, SSE pass-through edilir.
// Cok turlu gecmis STATELESS tasinir: istemci transkripti gonderir, input mesaj dizisi olur.
// (MAF Hosting.OpenAI previous_response_id/conversation'i COZMUYOR — store write-only,
// github.com/microsoft/agent-framework#3971; o yol olu, kullanma.)
public static class ChatEndpoints
{
    public sealed record ChatHistoryItem(string Role, string Content);
    public sealed record ChatRequest(string Message, List<ChatHistoryItem>? History);

    private static List<object> BuildInput(ChatRequest body)
    {
        var input = new List<object>();
        foreach (var h in body.History ?? [])
            if (h.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(h.Content))
                input.Add(new { role = h.Role, content = h.Content });
        input.Add(new { role = "user", content = body.Message });
        return input;
    }

    public static IEndpointRouteBuilder MapChatProxy(this IEndpointRouteBuilder app)
    {
        app.MapPost("/chat/stream", async (
            ChatRequest body,
            HttpContext http,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct) =>
        {
            var isAuthenticated = http.User.Identity?.IsAuthenticated == true;

            // Auth durumuna gore agent ve token. Anonim 'assistant'a ULASAMAZ.
            var (agentPath, agentName) = isAuthenticated
                ? ("/assistant/v1/responses", "assistant")
                : ("/public/v1/responses", "public");

            // Login ise user token forward edilir; anonim public agent yalniz storefront okur
            // (uclar AllowAnonymous) => bearer YOK, M2M client_credentials'a gerek kalmadi.
            var token = isAuthenticated
                ? await http.GetTokenAsync(OpenIdConnectParameterNames.AccessToken)
                  ?? throw new UnauthorizedAccessException("Access token bulunamadi.")
                : null;

            // OpenAI Responses govdesi. Gecmis + yeni mesaj tek input dizisinde (stateless).
            var payload = new Dictionary<string, object?>
            {
                ["model"] = agentName,
                ["input"] = BuildInput(body),
                ["stream"] = true,
            };

            var client = httpClientFactory.CreateClient("orchestrator");
            using var request = new HttpRequestMessage(HttpMethod.Post, agentPath);
            request.Content = JsonContent.Create(payload);
            if (token is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var upstream = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            http.Response.StatusCode = (int)upstream.StatusCode;
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            await using var upstreamStream = await upstream.Content.ReadAsStreamAsync(ct);
            await upstreamStream.CopyToAsync(http.Response.Body, ct);
        }).AllowAnonymous();

        // 032: admin onboarding kolu. YALNIZ admin rolu (cookie) — anonim/normal reddedilir (S3).
        // ChatAgent 'admin' persona'ya (/admin/v1/responses) proxy'ler; user token forward. Onboarding
        // gateway cagrisi ChatAgent icinde makine kimligiyle yapilir (admin token gateway'e gitmez).
        app.MapPost("/chat/admin/stream", async (
            ChatRequest body,
            HttpContext http,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync(OpenIdConnectParameterNames.AccessToken)
                        ?? throw new UnauthorizedAccessException("Access token bulunamadi.");

            var payload = new Dictionary<string, object?>
            {
                ["model"] = "admin",
                ["input"] = BuildInput(body),
                ["stream"] = true,
            };

            var client = httpClientFactory.CreateClient("orchestrator");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/v1/responses");
            request.Content = JsonContent.Create(payload);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var upstream = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            http.Response.StatusCode = (int)upstream.StatusCode;
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            await using var upstreamStream = await upstream.Content.ReadAsStreamAsync(ct);
            await upstreamStream.CopyToAsync(http.Response.Body, ct);
        }).RequireAuthorization(new AuthorizeAttribute { Roles = "admin" });

        return app;
    }
}