namespace ChatAgent.TokenHandlers;

// MCP'ye giden istege token takar. Istek baglaminda (HttpContext var): o anki isteğin
// Authorization header'i AYNEN forward edilir (login: user token; yoksa header gitmez).
// Acilis kesfinde (HttpContext yok): 061 korumali /mcp transport'lari kimlik istedigi icin
// DiscoveryTokenSource'tan makine token'i (chat-agent-discovery) takilir; config yoksa anonim.
// Yetki downstream handler middleware'inde (per-tool [RequiredScope]) kontrol edilir.
public sealed class TokenInjectingHandler(
    IHttpContextAccessor accessor,
    DiscoveryTokenSource discoveryToken) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("Authorization");

        var context = accessor.HttpContext;
        if (context is null)
        {
            var token = await discoveryToken.GetTokenAsync(cancellationToken);
            if (token is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            var incoming = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(incoming))
                request.Headers.TryAddWithoutValidation("Authorization", incoming);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
