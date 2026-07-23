namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// 006-home-storefront-list: ana sayfa tek okuma çağrısıyla bu listeden dolar (SC-001).
// Cache yok (K4): 5 sn tazelik hedefi var ve invalidasyon event-handler yoluna weave edilmiyor.
public static class GetStorefrontProductList
{
    public record GetStorefrontProductListQuery();

    public class StorefrontProductResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }

        // null = kaynak henüz raporlamadı ("bilinmiyor") — rozet çizilmez (FR-009).
        public int? StockQuantity { get; set; }
        public bool? IsInStock { get; set; }
        public decimal? DiscountRate { get; set; }

        public static StorefrontProductResponse From(StorefrontView view) => new()
        {
            ProductId = view.ProductId,
            Name = view.Name!,
            Description = view.Description ?? string.Empty,
            Brand = view.Brand ?? string.Empty,
            Price = view.Price!.Value,
            ImageUrl = view.ImageUrl,
            StockQuantity = view.StockQuantity,
            IsInStock = view.StockQuantity.HasValue ? view.StockQuantity > 0 : null,
            DiscountRate = view.DiscountRate
        };
    }

    public class GetStorefrontProductListQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<StorefrontProductResponse>>> Handle(
            GetStorefrontProductListQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            // Dolu-satır filtresi (K7): Price null'luğu "Catalog fat verisi gelmedi"nin işareti (FR-005).
            var views = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            var response = views.Select(StorefrontProductResponse.From).ToList();

            // Boş vitrin Ok kalır (US1-AS2); FeatureListResultModel bilinçli kullanılmadı (K3).
            return FeatureObjectResultModel<List<StorefrontProductResponse>>.Ok(response);
        }
    }
}

public static class GetStorefrontProductListEndpoint
{
    public static RouteGroupBuilder GetStorefrontProductListGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetStorefrontProductList.StorefrontProductResponse>>>(
                    new GetStorefrontProductList.GetStorefrontProductListQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetStorefrontProductList")
            .MapToApiVersion(1, 0)
            .Produces<List<GetStorefrontProductList.StorefrontProductResponse>>()
            .AllowAnonymous();

        return group;
    }
}