namespace WebApp.Authentication;

// 057: kullanici login DEGILSE Basket.Api cagrilarina X-Anonymous-Id header'i ekler
// (cookie'deki anonim Guid; yoksa uretilir). Login ise dokunmaz — kimlik token'daki sub'dan.
public class AnonymousBasketIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || httpContext.User.Identity?.IsAuthenticated == true)
            return await base.SendAsync(request, cancellationToken);

        request.Headers.TryAddWithoutValidation(AnonymousBasketId.HeaderName,
            AnonymousBasketId.GetOrCreate(httpContext).ToString());

        return await base.SendAsync(request, cancellationToken);
    }
}