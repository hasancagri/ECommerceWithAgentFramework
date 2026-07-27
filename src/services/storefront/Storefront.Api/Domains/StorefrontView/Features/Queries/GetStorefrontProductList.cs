namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// 006-home-storefront-list: ana sayfa tek okuma çağrısıyla bu listeden dolar (SC-001).
// 011: liste sayfalıdır; ana sayfa pageSize=8, Tüm Ürünler pageSize=12 ile aynı query'yi kullanır.
// Cache yok (K4): 5 sn tazelik hedefi var ve invalidasyon event-handler yoluna weave edilmiyor.
// 016: opsiyonel kategori/marka filtreleri (Id öncelikli, ad yedek); AND ile birleşir (US1/US2).
public static class GetStorefrontProductList
{
    public const int DefaultPageSize = 12;

    public record GetStorefrontProductListQuery(
        int PageNumber = 1,
        int PageSize = DefaultPageSize,
        Guid? CategoryId = null,
        string? Category = null,
        Guid? BrandId = null,
        string? Brand = null)
    {
        // 011 FR-005: 1'den küçük değerler 1. sayfaya/varsayılan boyuta normalize edilir.
        public static int NormalizePageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;
        public static int NormalizePageSize(int pageSize) => pageSize < 1 ? DefaultPageSize : pageSize;
    }

    // Saf, test edilebilir filtre çekirdeği: Id verilmişse Id, yoksa ad eşleşmesi; filtreler AND'lenir.
    public static IQueryable<StorefrontView> ApplyFilters(
        IQueryable<StorefrontView> source,
        Guid? categoryId, string? category, Guid? brandId, string? brand)
    {
        if (categoryId is not null)
            source = source.Where(x => x.CategoryId == categoryId);
        else if (!string.IsNullOrWhiteSpace(category))
            source = source.Where(x => x.Category == category);

        if (brandId is not null)
            source = source.Where(x => x.BrandId == brandId);
        else if (!string.IsNullOrWhiteSpace(brand))
            source = source.Where(x => x.Brand == brand);

        return source;
    }

    public class StorefrontProductResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public Guid? BrandId { get; set; }
        public string Brand { get; set; } = null!;
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }
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
            BrandId = view.BrandId,
            Brand = view.Brand ?? string.Empty,
            CategoryId = view.CategoryId,
            Category = view.Category,
            Price = view.Price!.Value,
            ImageUrl = view.ImageUrl,
            StockQuantity = view.StockQuantity,
            IsInStock = view.StockQuantity.HasValue ? view.StockQuantity > 0 : null,
            DiscountRate = view.DiscountRate
        };
    }

    public class GetStorefrontProductListQueryHandler
    {
        public async Task<FeaturePagedResultModel<StorefrontProductResponse>> Handle(
            GetStorefrontProductListQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var pageNumber = GetStorefrontProductListQuery.NormalizePageNumber(query.PageNumber);
            var pageSize = GetStorefrontProductListQuery.NormalizePageSize(query.PageSize);

            // Dolu-satır filtresi (K7): Price null'luğu "Catalog fat verisi gelmedi"nin işareti (FR-005/006-K7).
            var filtered = ApplyFilters(
                session.Query<StorefrontView>()
                    .Where(x => !x.IsDeleted && x.Name != null && x.Price != null),
                query.CategoryId, query.Category, query.BrandId, query.Brand);

            // Sayfa sayısı filtreli sonuca göre hesaplanır (US1-2/SC-005).
            var totalCount = await filtered.CountAsync(ct);

            var views = await filtered
                .OrderBy(x => x.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var response = views.Select(StorefrontProductResponse.From).ToList();

            // Boş vitrin ve aralık dışı sayfa NotFound döner; WebApp bunu boş duruma çevirir (011 FR-006).
            var metaData = new StaticPagedList<StorefrontProductResponse>(response, pageNumber, pageSize, totalCount);
            return FeaturePagedResultModel<StorefrontProductResponse>.Ok(metaData, response);
        }
    }
}

public static class GetStorefrontProductListEndpoint
{
    public static RouteGroupBuilder GetStorefrontProductListGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus,
                int page = 1,
                int pageSize = GetStorefrontProductList.DefaultPageSize,
                Guid? categoryId = null,
                string? category = null,
                Guid? brandId = null,
                string? brand = null) =>
            {
                var result = await bus.InvokeAsync<FeaturePagedResultModel<GetStorefrontProductList.StorefrontProductResponse>>(
                    new GetStorefrontProductList.GetStorefrontProductListQuery(
                        page, pageSize, categoryId, category, brandId, brand));

                // Sayfa meta'sı (toplam kayıt/sayfa) istemciye lazım; Data yerine result'ın tamamı döner.
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetStorefrontProductList")
            .MapToApiVersion(1, 0)
            .Produces<FeaturePagedResultModel<GetStorefrontProductList.StorefrontProductResponse>>()
            .AllowAnonymous();

        return group;
    }
}