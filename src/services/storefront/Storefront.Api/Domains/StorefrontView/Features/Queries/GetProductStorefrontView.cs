namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

public static class GetProductStorefrontView
{
    [RequiredScope(AuthorizationScopes.StorefrontRead)]
    public record GetProductStorefrontViewQuery(Guid ProductId);

    public class ProductStorefrontViewResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public bool IsDeleted { get; set; }

        // null = kaynak henuz raporlamadi (kismi satir, FR-008) — "bilinmiyor" anlamina gelir.
        public bool? IsInStock { get; set; }
        public decimal? DiscountRate { get; set; }

        public static ProductStorefrontViewResponse From(CatalogInfo catalog, StockInfo? stock, DiscountInfo? discount) => new()
        {
            ProductId = catalog.ProductId,
            Name = catalog.Name,
            ImageUrl = catalog.ImageUrl,
            IsDeleted = catalog.IsDeleted,
            IsInStock = stock?.IsInStock,
            DiscountRate = discount?.Rate
        };
    }

    public class GetProductStorefrontViewQueryHandler
    {
        public async Task<FeatureObjectResultModel<ProductStorefrontViewResponse>> Handle(
            GetProductStorefrontViewQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            // 3 LoadAsync de Storefront'un KENDI veritabanina yapilir — kaynak servislere senkron
            // cagri YOK (FR-002/003). Tek "yok" durumu CatalogInfo bulunamadiginda olusur.
            var catalog = await session.LoadAsync<CatalogInfo>(query.ProductId, ct);
            if (catalog is null)
                return FeatureObjectResultModel<ProductStorefrontViewResponse>.NotFound();

            var stock = await session.LoadAsync<StockInfo>(query.ProductId, ct);
            var discount = await session.LoadAsync<DiscountInfo>(query.ProductId, ct);

            return FeatureObjectResultModel<ProductStorefrontViewResponse>.Ok(
                ProductStorefrontViewResponse.From(catalog, stock, discount));
        }
    }
}

public static class GetProductStorefrontViewEndpoint
{
    public static RouteGroupBuilder GetProductStorefrontViewGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductStorefrontView.ProductStorefrontViewResponse>>(
                    new GetProductStorefrontView.GetProductStorefrontViewQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductStorefrontView")
            .MapToApiVersion(1, 0)
            .Produces<GetProductStorefrontView.ProductStorefrontViewResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationScopes.StorefrontRead);

        return group;
    }
}