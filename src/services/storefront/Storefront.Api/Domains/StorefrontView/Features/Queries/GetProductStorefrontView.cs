namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

public static class GetProductStorefrontView
{
    [RequiredScope(AuthorizationScopes.StorefrontRead)]
    public record GetProductStorefrontViewQuery(Guid ProductId);

    public class ProductStorefrontViewResponse
    {
        public Guid ProductId { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsDeleted { get; set; }

        // null = kaynak henuz raporlamadi (kismi satir, FR-008) — "bilinmiyor" anlamina gelir.
        public int? StockQuantity { get; set; }
        public bool? IsInStock { get; set; }
        public decimal? DiscountRate { get; set; }

        public static ProductStorefrontViewResponse From(StorefrontView view) => new()
        {
            ProductId = view.ProductId,
            Name = view.Name,
            ImageUrl = view.ImageUrl,
            IsDeleted = view.IsDeleted,
            StockQuantity = view.StockQuantity,
            IsInStock = view.StockQuantity.HasValue ? view.StockQuantity > 0 : null,
            DiscountRate = view.DiscountRate
        };
    }

    public class GetProductStorefrontViewQueryHandler
    {
        public async Task<FeatureObjectResultModel<ProductStorefrontViewResponse>> Handle(
            GetProductStorefrontViewQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            // Tek LoadAsync Storefront'un KENDI veritabanina yapilir — kaynak servislere senkron
            // cagri YOK (FR-002/003). "Yok" durumu satir hic olusmadiginda olusur.
            var view = await session.LoadAsync<StorefrontView>(query.ProductId, ct);
            if (view is null)
                return FeatureObjectResultModel<ProductStorefrontViewResponse>.NotFound();

            return FeatureObjectResultModel<ProductStorefrontViewResponse>.Ok(
                ProductStorefrontViewResponse.From(view));
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