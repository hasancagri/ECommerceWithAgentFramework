namespace Basket.Api.Domains.Baskets;

public static class BasketEndpointExtension
{
    public static void AddBasketGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/baskets")
            .WithTags("Baskets")
            .WithApiVersionSet(apiVersionSet)
            .AddBasketItemGroupItemEndpoint()
            .DeleteBasketItemGroupItemEndpoint()
            .GetBasketGroupItemEndpoint()
            .ApplyDiscountCouponGroupItemEndpoint()
            .RemoveDiscountCouponGroupItemEndpoint()
            .RequireAuthorization();
    }
}