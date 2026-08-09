using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace WebApp.Chat;

// Tarayicidaki chat widget'i ile ChatAgent arasindaki BFF proxy.
// Token HttpOnly cookie'de oldugundan tarayici orchestrator'a dogrudan erisemez;
// burada auth durumuna gore agent + token secilir, SSE pass-through edilir.
public static class ChatEndpoints
{
    public sealed record ChatRequest(string Message, string? PreviousResponseId);

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

            // OpenAI Responses govdesi. Cok turlu gecmis previous_response_id ile zincirlenir
            // (orchestrator tarafinda RAM'de tutulur).
            var payload = new Dictionary<string, object?>
            {
                ["model"] = agentName,
                ["input"] = body.Message,
                ["stream"] = true,
            };
            if (!string.IsNullOrWhiteSpace(body.PreviousResponseId))
                payload["previous_response_id"] = body.PreviousResponseId;

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
                ["input"] = body.Message,
                ["stream"] = true,
            };
            if (!string.IsNullOrWhiteSpace(body.PreviousResponseId))
                payload["previous_response_id"] = body.PreviousResponseId;

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