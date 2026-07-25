namespace Order.Api.Grpc;

// 012: giden gRPC cagrisina, gelen HTTP istegindeki kullanici bearer token'ini tasir
// (Order -> Stock Commit yetkisi). Token stock.reserve scope'unu icermeli (G2).
public sealed class BearerForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        return base.SendAsync(request, cancellationToken);
    }
}