using System.Net.Http.Headers;

namespace AgentOrchestrator;

// MCP'ye giden her istege token iliştirir: o anki isteğin kullanici token'i varsa onu
// (per-user), yoksa m2m client_credentials token'i (anonim/acilis kesfi). Yetki downstream
// handler middleware'inde (per-tool scope) kontrol edilir.
public sealed class TokenInjectingHandler(
    IHttpContextAccessor accessor,
    IClientCredentialsTokenProvider clientCredentials) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = null;

        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            request.Headers.TryAddWithoutValidation("Authorization", incoming);
        }
        else
        {
            var token = await clientCredentials.GetTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}