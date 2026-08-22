namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// 016 US1-3/R8: filtre seçenekleri (facet) satılabilir satırlardan türetilir — ürünü olmayan
// kategori/marka kendiliğinden görünmez; kategorisi null satır kategori listesine girmez.
// Filtre seçenekleri cache'lidir (sabit anahtar, herkese aynı); boşaltma ProductChangedEvent
// handler'ında (CacheInvalidator, projeksiyon-BC kuralı) + 60sn TTL güvenlik ağı.
// Liste/tekil ürün query'leri bilinçli cache'siz: kardinalite + yazma-yolu kuralı (CLAUDE.md). Okuma anonim.
public static class GetStorefrontFilterOptions
{
    [Cached("filters", 60)]
    public record GetStorefrontFilterOptionsQuery();

    public class FilterOptionResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class StorefrontFilterOptionsResponse
    {
        public List<FilterOptionResponse> Categories { get; set; } = [];
        public List<FilterOptionResponse> Brands { get; set; } = [];

        // 043: spec facet'leri — Name + option'lar + ürün sayısı (count birebirlik SC-006).
        public List<SpecFacetResponse> Specifications { get; set; } = [];
    }

    public class SpecFacetResponse
    {
        public string Name { get; set; } = null!;
        public List<SpecFacetOptionResponse> Options { get; set; } = [];
    }

    public class SpecFacetOptionResponse
    {
        public string Name { get; set; } = null!;
        public int Count { get; set; }
    }

    // Saf, test edilebilir çekirdek: Distinct kimlik+ad çiftleri, ada göre sıralı.
    public static StorefrontFilterOptionsResponse BuildOptions(IEnumerable<StorefrontView> sellableRows)
    {
        var rows = sellableRows.ToList();

        var categories = rows
            .Where(x => x.CategoryId is not null && !string.IsNullOrWhiteSpace(x.Category))
            .GroupBy(x => x.CategoryId!.Value)
            .Select(g => new FilterOptionResponse { Id = g.Key, Name = g.First().Category! })
            .OrderBy(x => x.Name)
            .ToList();

        var brands = rows
            .Where(x => x.BrandId is not null && !string.IsNullOrWhiteSpace(x.Brand))
            .GroupBy(x => x.BrandId!.Value)
            .Select(g => new FilterOptionResponse { Id = g.Key, Name = g.First().Brand! })
            .OrderBy(x => x.Name)
            .ToList();

        // 043: spec facet'leri satırlardaki ad çiftlerinden türetilir; 045: count = çifti taşıyan
        // DISTINCT AİLE sayısı (kart-bazlı — 3 üyeli aile 1 sayılır; SC-003 liste kartıyla birebir).
        // Ailesiz satır FamilyKey=ProductId (tekil) → 043 davranışıyla aynı kalır (regresyon 0).
        var specifications = rows
            .SelectMany(x => x.Specs.Select(s =>
                (Family: GetStorefrontProductList.FamilyKey(x), s.Attribute, s.Option)))
            .GroupBy(p => p.Attribute)
            .Select(g => new SpecFacetResponse
            {
                Name = g.Key,
                Options = g.GroupBy(p => p.Option)
                    .Select(o => new SpecFacetOptionResponse
                    { Name = o.Key, Count = o.Select(p => p.Family).Distinct().Count() })
                    .OrderBy(o => o.Name)
                    .ToList(),
            })
            .OrderBy(s => s.Name)
            .ToList();

        return new StorefrontFilterOptionsResponse
        { Categories = categories, Brands = brands, Specifications = specifications };
    }

    public class GetStorefrontFilterOptionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<StorefrontFilterOptionsResponse>> Handle(
            GetStorefrontFilterOptionsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            // Liste ile aynı satılabilirlik (dolu-satır) filtresi — facet ile sonuç tutarlı kalır.
            var rows = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            return FeatureObjectResultModel<StorefrontFilterOptionsResponse>.Ok(BuildOptions(rows));
        }
    }
}

public static class GetStorefrontFilterOptionsEndpoint
{
    public static RouteGroupBuilder GetStorefrontFilterOptionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/filters", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetStorefrontFilterOptions.StorefrontFilterOptionsResponse>>(
                    new GetStorefrontFilterOptions.GetStorefrontFilterOptionsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetStorefrontFilterOptions")
            .MapToApiVersion(1, 0)
            .AllowAnonymous();

        return group;
    }
}