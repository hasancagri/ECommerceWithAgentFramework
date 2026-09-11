namespace Ucp.Api.CatalogProjection;

/// <summary>
/// 072 US2: satılabilir ürün keşfi — kimlikle getirme + arama. Kanalın KENDİ read projeksiyonundan
/// (UcpCatalogItem) sunulur (FR-012; İlke I). Anonim (platform kapıyı/ürünleri auth'tan önce tanır).
/// </summary>
public static class UcpCatalogEndpointExtension
{
    public record CatalogItemDto(string Id, string Title, decimal Price, bool Available);

    public static void AddUcpCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("ucp/catalog").WithTags("UcpCatalog");

        // lookup: GET /ucp/catalog/{id}
        group.MapGet("{id}", async (string id, IQuerySession session, CancellationToken ct) =>
        {
            var item = await session.LoadAsync<UcpCatalogItem>(id, ct);
            return item is null
                ? Results.NotFound()
                : Results.Ok(new CatalogItemDto(item.Id, item.Title, item.Price, item.Available));
        });

        // search: GET /ucp/catalog?q=kelime  (yalnız satılabilir; en çok 20 sonuç)
        group.MapGet("", async (string? q, IQuerySession session, CancellationToken ct) =>
        {
            var query = session.Query<UcpCatalogItem>().Where(x => x.Available);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.ToLowerInvariant();
                query = query.Where(x => x.SearchText.Contains(needle));
            }

            var items = await query.Take(20).ToListAsync(ct);
            return Results.Ok(items.Select(i => new CatalogItemDto(i.Id, i.Title, i.Price, i.Available)).ToList());
        });
    }
}
