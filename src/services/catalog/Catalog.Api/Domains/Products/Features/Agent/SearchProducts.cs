using Common;
using Common.Utils.Authorization;
using Common.Utils.Constants;

namespace Catalog.Api.Domains.Products.Features.Agent;

// MCP 'search_products' tool'unun arkasindaki ajan-ozel sorgu.
// Sadece GOSTERME/ARAMA icin kullanilir: isimle aktif urunu bulur ve kullaniciyi
// urun detay sayfasina goturecek linki doner (baska alan YOK).
public static class SearchProducts
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record SearchProductsQuery(string Name);

    // Yalnizca detay sayfasi linki.
    // Not: FeatureObjectResultModel<T> 'new()' kisiti istedigi icin parametresiz ctor sart.
    public class SearchProductResponse
    {
        public string DetailUrl { get; set; } = null!;
    }

    public class SearchProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<SearchProductResponse>> Handle(
            SearchProductsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Id'yi kontratta acmayiz; anonymous tiple cekip yalnizca link uretmek icin kullaniriz.
            var row = await session.Query<Product>()
                .Where(x => !x.IsDeleted && x.IsActive &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync(ct);

            var response = row is null
                ? null
                : new SearchProductResponse { DetailUrl = $"/Products/Detail/{row.Id}" };

            // Ok(null) -> FeatureObjectResultModel otomatik NotFound doner.
            return FeatureObjectResultModel<SearchProductResponse>.Ok(response);
        }
    }
}