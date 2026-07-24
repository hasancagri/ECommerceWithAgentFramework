using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WebApp.Authentication;

namespace WebApp.Chat;

// Tarayicidaki chat widget'i ile ChatAgent arasindaki BFF proxy (009: conversation-id akisi).
// Token HttpOnly cookie'de oldugundan tarayici orchestrator'a dogrudan erisemez; burada auth
// durumuna gore agent + token secilir. Gecmis sunucuda yasar; tarayici yalnizca id tasir.
public static class ChatEndpoints
{
    public sealed record ChatRequest(string Message, string? ConversationId);

    public static IEndpointRouteBuilder MapChatProxy(this IEndpointRouteBuilder app)
    {
        // Yeni konusma: agent auth durumuna gore secilir; login'de sahiplik ChatAgent'ta token'dan yazilir.
        app.MapPost("/chat/conversations", async (
            HttpContext http, IHttpClientFactory httpClientFactory, TokenService tokenService,
            CancellationToken ct) =>
        {
            var conversationId = await CreateConversationAsync(http, httpClientFactory, tokenService, ct);
            return Results.Ok(new { conversationId });
        }).AllowAnonymous();

        // Gecmis konusma listesi (yalniz login) — ChatAgent sahibine gore suzer.
        app.MapGet("/chat/conversations", async (
            HttpContext http, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            if (http.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var client = httpClientFactory.CreateClient("orchestrator");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/my-conversations?page=1&pageSize=50");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await UserTokenAsync(http));

            using var upstream = await client.SendAsync(request, ct);
            return Results.Content(await upstream.Content.ReadAsStringAsync(ct),
                "application/json", statusCode: (int)upstream.StatusCode);
        }).AllowAnonymous();

        // Tek konusmanin TAM gecmisi (yalniz login; sahip degilse ChatAgent 404 doner).
        app.MapGet("/chat/conversations/{id}", async (
            string id, HttpContext http, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            if (http.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var client = httpClientFactory.CreateClient("orchestrator");
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/v1/my-conversations/{Uri.EscapeDataString(id)}/items?page=1&pageSize=500");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await UserTokenAsync(http));

            using var upstream = await client.SendAsync(request, ct);
            return Results.Content(await upstream.Content.ReadAsStringAsync(ct),
                "application/json", statusCode: (int)upstream.StatusCode);
        }).AllowAnonymous();

        app.MapPost("/chat/stream", async (
            ChatRequest body, HttpContext http, IHttpClientFactory httpClientFactory,
            TokenService tokenService, CancellationToken ct) =>
        {
            var token = await ResolveTokenAsync(http, tokenService);
            var client = httpClientFactory.CreateClient("orchestrator");

            // Id yoksa/bayatsa yeni konusma acilir (FR-010); yeni id SSE'den ONCE header'la bildirilir.
            var conversationId = body.ConversationId;
            var upstream = conversationId is null
                ? null
                : await SendChatAsync(client, token, conversationId, body.Message, ct);

            if (upstream is null || upstream.StatusCode == HttpStatusCode.NotFound)
            {
                upstream?.Dispose();
                conversationId = await CreateConversationAsync(http, httpClientFactory, tokenService, ct);
                upstream = await SendChatAsync(client, token, conversationId!, body.Message, ct);
            }

            using var response = upstream;
            http.Response.StatusCode = (int)response.StatusCode;
            http.Response.Headers["X-Conversation-Id"] = conversationId;
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            await using var upstreamStream = await response.Content.ReadAsStreamAsync(ct);
            await upstreamStream.CopyToAsync(http.Response.Body, ct);
        }).AllowAnonymous();

        return app;
    }

    private static async Task<string?> CreateConversationAsync(
        HttpContext http, IHttpClientFactory httpClientFactory, TokenService tokenService, CancellationToken ct)
    {
        var isAuthenticated = http.User.Identity?.IsAuthenticated == true;
        var agentName = isAuthenticated ? "assistant" : "public"; // anonim 'assistant'a ULASAMAZ

        var client = httpClientFactory.CreateClient("orchestrator");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/my-conversations");
        request.Content = JsonContent.Create(new { agentName });
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await ResolveTokenAsync(http, tokenService));

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CreateConversationResponse>(ct);
        return payload?.ConversationId;
    }

    private static async Task<HttpResponseMessage> SendChatAsync(
        HttpClient client, string token, string conversationId, string message, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat");
        request.Content = JsonContent.Create(new { conversationId, message });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static async Task<string> ResolveTokenAsync(HttpContext http, TokenService tokenService)
        => http.User.Identity?.IsAuthenticated == true
            ? await UserTokenAsync(http)
            : (await tokenService.GetClientAccessTokenAsync()).AccessToken!;

    private static async Task<string> UserTokenAsync(HttpContext http)
        => await http.GetTokenAsync(OpenIdConnectParameterNames.AccessToken)
           ?? throw new UnauthorizedAccessException("Access token bulunamadi.");

    private sealed record CreateConversationResponse(string ConversationId);
}