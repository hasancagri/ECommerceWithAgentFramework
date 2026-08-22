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
        string? Brand = null,
        // 043: "Attribute|Option" anahtarları; aynı attribute OR, farklı attribute AND (FR-008).
        string[]? Specs = null)
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

    // 043: "Attribute|Option" anahtarlarını attribute-gruplarına ayırır; geçersiz girdi yok sayılır
    // (kontrat: storefront-filter-api.md). Sıra korunur (deterministik SQL üretimi).
    public static Dictionary<string, string[]> ParseSpecGroups(IReadOnlyList<string> specs)
    {
        return specs
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => (Raw: s, Parts: s.Split('|')))
            .Where(x => x.Parts.Length == 2
                        && !string.IsNullOrWhiteSpace(x.Parts[0])
                        && !string.IsNullOrWhiteSpace(x.Parts[1]))
            .GroupBy(x => x.Parts[0])
            .ToDictionary(g => g.Key, g => g.Select(x => x.Raw).Distinct().ToArray());
    }

    // 043: bellek-içi semantik çekirdeği (birim testin sözleşmesi) — grup içi OR, gruplar arası AND.
    // SQL yolu (aşağıda MatchesSql jsonb ?|) aynı semantiği Postgres'te uygular; canlı doğrulama eşler.
    public static bool MatchesSpecFilters(StorefrontView view, Dictionary<string, string[]> groups) =>
        groups.Values.All(keys => keys.Any(k => view.SpecKeys.Contains(k)));

    // 043 R6: attribute-grubu başına jsonb ?| (herhangi biri) — LINQ iç-içe Any/OR kompozisyonu
    // yerine MatchesSql (040 dersi). Gruplar zincirli Where ile AND'lenir.
    // '^' placeholder şart: Weasel '??' kaçışını desteklemez, düz '?' operatörle çakışır (canlıda kanıtlandı).
    // (object) cast şart: string[] params-dizisine yayılırsa tek text[] yerine N ayrı text parametresi biner.
    public static IQueryable<StorefrontView> ApplySpecFilters(
        IQueryable<StorefrontView> source, Dictionary<string, string[]> groups)
    {
        foreach (var keys in groups.Values)
            source = source.Where(x => x.MatchesSql('^', "d.data -> 'SpecKeys' ?| ^", (object)keys));
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

        // 044: kart yıldız rozeti — null/0 = çizilmez (FR-006).
        public decimal? RatingAverage { get; set; }
        public int RatingCount { get; set; }

        // 045: ailenin görünür üye adedi (ailesizde 1); >1 ise kart "N varyant" rozeti çizer.
        public int VariantCount { get; set; }

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
            RatingAverage = view.RatingAverage,
            RatingCount = view.RatingCount
        };
    }

    // 045: liste gruplama anahtarı — ailesizde ProductId (aile kadar tekil), ailede FamilyCode.
    public static string FamilyKey(StorefrontView v) =>
        string.IsNullOrWhiteSpace(v.FamilyCode) ? v.ProductId.ToString() : v.FamilyCode!;

    // 045: temsilci — stok>0 önce, sonra en ucuz, sonra ProductId (deterministik; FR-007 + Assumption).
    public static StorefrontView PickRepresentative(IReadOnlyCollection<StorefrontView> members) =>
        members
            .OrderByDescending(m => m.StockQuantity is > 0)
            .ThenBy(m => m.Price ?? decimal.MaxValue)
            .ThenBy(m => m.ProductId)
            .First();

    public record RepresentativeGroup(StorefrontView Representative, int VariantCount);

    // 045: filtreli küme → aile başına TEK temsilci + görünür üye adedi (SC-003 kart-bazlı).
    // Filtre-bağlamlı: yalnız filtreyi geçen üyeler girer → temsilci eşleşen üyeye kayar (FR-009).
    public static IReadOnlyList<RepresentativeGroup> GroupToRepresentatives(IEnumerable<StorefrontView> views) =>
        views
            .GroupBy(FamilyKey)
            .Select(g => new RepresentativeGroup(PickRepresentative(g.ToList()), g.Count()))
            .ToList();

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

            // 043: spec kesişimi — grup içi OR, gruplar arası AND (FR-008).
            filtered = ApplySpecFilters(filtered, ParseSpecGroups(query.Specs ?? []));

            // 045: filtreleme DB'de (test edilmiş LINQ/jsonb SQL); aile gruplama + temsilci bellekte
            // (yüzlerce ürün ölçeği — DISTINCT ON raw SQL 043 kırılganlığını geri getirirdi, ELENDİ).
            var members = await filtered.ToListAsync(ct);
            var groups = GroupToRepresentatives(members)
                .OrderBy(g => g.Representative.Name)
                .ToList();

            // Sayfalama KART bazlı (aile=1 kart); toplam = aile sayısı (SC-003).
            var totalCount = groups.Count;
            var response = groups
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(g =>
                {
                    var r = StorefrontProductResponse.From(g.Representative);
                    r.VariantCount = g.VariantCount;
                    return r;
                })
                .ToList();

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
                string? brand = null,
                // 043: çoklu spec anahtarı (?spec=Renk|Siyah&spec=Materyal|Çelik)
                [FromQuery(Name = "spec")] string[]? spec = null) =>
            {
                var result = await bus.InvokeAsync<FeaturePagedResultModel<GetStorefrontProductList.StorefrontProductResponse>>(
                    new GetStorefrontProductList.GetStorefrontProductListQuery(
                        page, pageSize, categoryId, category, brandId, brand, spec));

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