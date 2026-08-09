namespace ChatAgent.TokenHandlers;

// MCP'ye giden her istege, o anki isteğin Authorization header'ini forward eder.
// Token'i WebApp BFF saglar (anonim: ecommerce.bff client_credentials; login: user token).
// Acilis kesfinde (istek yok) token eklenmez => ListTools anonim calisir (transport acik).
// Yetki downstream handler middleware'inde (per-tool [RequiredScope]) kontrol edilir.
public sealed class TokenInjectingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("Authorization");

        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
            request.Headers.TryAddWithoutValidation("Authorization", incoming);

        return await base.SendAsync(request, cancellationToken);
    }
}