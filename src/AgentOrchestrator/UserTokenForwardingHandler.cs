namespace AgentOrchestrator;

sealed class UserTokenForwardingHandler(IHttpContextAccessor accessor, string fallbackToken) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();

        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            !string.IsNullOrWhiteSpace(incoming) ? incoming : $"Bearer {fallbackToken}");

        return base.SendAsync(request, cancellationToken);
    }
}