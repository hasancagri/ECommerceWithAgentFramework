namespace Catalog.Api.Domains.Products;

public static class ProductEndpointExtension
{
    public static void AddProductGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // Vitrin okuması Storefront'tadır; buradaki GET, aggregate'in yönetim penceresidir (2026-08-19 kuralı).
        // 058 FR-010: tüm pencere catalog.write scope'uyla korunur (admin yönetim yüzeyi; anonim tüketici yok).
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Products").WithApiVersionSet(apiVersionSet)
            .CreateProductGroupItemEndpoint()
            .UpdateProductGroupItemEndpoint()
            .SetProductDimensionsGroupItemEndpoint()
            .SetProductSeoGroupItemEndpoint()
            .SetProductPublishedGroupItemEndpoint()
            .AssignTagToProductGroupItemEndpoint()
            .RemoveTagFromProductGroupItemEndpoint()
            .GetProductByIdGroupItemEndpoint()
            .AdminListProductsGroupItemEndpoint()
            .AdminGetProductGroupItemEndpoint()
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);

        // 059: fiyat geçmişi müşteri-yüzü okuma — korumalı grubun DIŞINDA, anonim (detay sayfası login'siz).
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Products").WithApiVersionSet(apiVersionSet)
            .GetProductPriceHistoryGroupItemEndpoint();
    }
}