using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WebApp.Authentication;

namespace WebApp.Chat;

// Tarayicidaki chat widget'i ile AgentOrchestrator arasindaki BFF proxy.
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
            TokenService tokenService,
            CancellationToken ct) =>
        {
            var isAuthenticated = http.User.Identity?.IsAuthenticated == true;

            // Auth durumuna gore agent ve token. Anonim 'assistant'a ULASAMAZ.
            var (agentPath, agentName) = isAuthenticated
                ? ("/assistant/v1/responses", "assistant")
                : ("/public/v1/responses", "public");

            var token = isAuthenticated
                ? await http.GetTokenAsync(OpenIdConnectParameterNames.AccessToken)
                  ?? throw new UnauthorizedAccessException("Access token bulunamadi.")
                : (await tokenService.GetClientAccessTokenAsync()).AccessToken!;

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

        return app;
    }
}