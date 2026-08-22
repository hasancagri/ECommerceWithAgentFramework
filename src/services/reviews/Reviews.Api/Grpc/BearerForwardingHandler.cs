namespace Reviews.Api.Grpc;

// 044: giden gRPC cagrisina, gelen HTTP istegindeki kullanici bearer token'ini tasir
// (Reviews -> Order satin-alma kaniti; 012 Basket emsalinin BC-ici kopyasi — bilinçli tekrar).
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
