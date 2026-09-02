namespace Basket.Api.Domains.Baskets;

public static class BasketEndpointExtension
{
    // 057: sepet uclari anonim erisilebilir — grup auth'u yok. Sahip kimligi token'dan (sub)
    // ya da WebApp'in tasidigi X-Anonymous-Id header'indan cozulur. Merge ucu istisna: token ister.
    public const string AnonymousIdHeader = "X-Anonymous-Id";

    public static void AddBasketGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/baskets")
            .WithTags("Baskets")
            .WithApiVersionSet(apiVersionSet)
            .AddBasketItemGroupItemEndpoint()
            .SetBasketItemQuantityGroupItemEndpoint()
            .DeleteBasketItemGroupItemEndpoint()
            .GetBasketGroupItemEndpoint()
            .MergeBasketGroupItemEndpoint();
    }

    // Sepet sahibi: login ise sub claim'i, degilse header'daki anonim Guid; ikisi de yoksa Guid.Empty.
    public static Guid ResolveOwnerId(HttpContext httpContext, ICurrentUser currentUser)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
            return currentUser.Load(httpContext.User).Id;

        return Guid.TryParse(httpContext.Request.Headers[AnonymousIdHeader], out var anonymousId)
            ? anonymousId
            : Guid.Empty;
    }
}