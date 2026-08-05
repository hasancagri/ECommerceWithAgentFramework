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

        return new StorefrontFilterOptionsResponse { Categories = categories, Brands = brands };
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