namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

public static class GetProductStorefrontView
{
    public record GetProductStorefrontViewQuery(Guid ProductId);

    public class ProductStorefrontViewResponse
    {
        public Guid ProductId { get; set; }
        public string? Name { get; set; }
        // Urun detayi vitrinden beslenir: aciklama + fiyat da tasinir. null = Catalog henuz raporlamadi.
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsDeleted { get; set; }

        // 052: künye — tüm yazarlar (liste) + tek yayınevi. null/boş = Catalog henüz raporlamadı.
        public List<AuthorRef> Authors { get; set; } = [];
        public Guid? PublisherId { get; set; }
        public string? Publisher { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }

        // null = kaynak henuz raporlamadi (kismi satir, FR-008) — "bilinmiyor" anlamina gelir.
        public int? StockQuantity { get; set; }
        public bool? IsInStock { get; set; }

        // 044: puan ozeti — null/0 = rozet cizilmez (FR-006).
        public decimal? RatingAverage { get; set; }
        public int RatingCount { get; set; }

        // 043 US2: detay spec tablosu — kanonik (attribute, option) ciftleri; bos liste mesru.
        public List<ProductSpecResponse> Specs { get; set; } = [];

        public class ProductSpecResponse
        {
            public string Attribute { get; set; } = null!;
            public string Option { get; set; } = null!;
        }

        public static ProductStorefrontViewResponse From(StorefrontView view) => new()
        {
            ProductId = view.ProductId,
            Name = view.Name,
            Description = view.Description,
            Price = view.Price,
            ImageUrl = view.ImageUrl,
            IsDeleted = view.IsDeleted,
            Authors = view.Authors,
            PublisherId = view.PublisherId,
            Publisher = view.Publisher,
            CategoryId = view.CategoryId,
            Category = view.Category,
            StockQuantity = view.StockQuantity,
            IsInStock = view.StockQuantity.HasValue ? view.StockQuantity > 0 : null,
            RatingAverage = view.RatingAverage,
            RatingCount = view.RatingCount,
            Specs = view.Specs
                .Select(s => new ProductSpecResponse { Attribute = s.Attribute, Option = s.Option })
                .ToList()
        };
    }

    public class GetProductStorefrontViewQueryHandler
    {
        public async Task<FeatureObjectResultModel<ProductStorefrontViewResponse>> Handle(
            GetProductStorefrontViewQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
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
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}