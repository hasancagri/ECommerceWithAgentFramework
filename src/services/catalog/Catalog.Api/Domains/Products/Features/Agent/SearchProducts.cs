namespace Catalog.Api.Domains.Products.Features.Agent;

public static class SearchProducts
{
    public record SearchProductsQuery(string Name);

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
            var row = await session.Query<Product>()
                .Where(x => !x.IsDeleted && x.IsActive && x.IsComplete &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync(ct);

            var response = row is null
                ? null
                : new SearchProductResponse { DetailUrl = $"/Products/Detail/{row.Id}" };

            return FeatureObjectResultModel<SearchProductResponse>.Ok(response);
        }
    }
}