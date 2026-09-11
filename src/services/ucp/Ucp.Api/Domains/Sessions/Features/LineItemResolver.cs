using Ucp.Api.CatalogProjection;

namespace Ucp.Api.Domains.Sessions.Features;

/// <summary>
/// Talep edilen {product_id, quantity} kalemlerini UcpCatalogItem projeksiyonundan çözer — başlık +
/// fiyat SUNUCU-otoritesi (dış platform fiyat set edemez; güvenlik). Bulunamayan/satılamaz ürün
/// mesajla atlanır (session hataya düşmez). Fiyat decimal TRY → minor units (×100).
/// </summary>
public static class LineItemResolver
{
    public record RequestedLine(string ProductId, int Quantity);

    public record Resolved(List<UcpLineItem> Items, List<MessageItem> Messages);

    public static async Task<Resolved> ResolveAsync(
        IReadOnlyList<RequestedLine> requested, IQuerySession session, CancellationToken ct)
    {
        var items = new List<UcpLineItem>();
        var messages = new List<MessageItem>();

        foreach (var line in requested)
        {
            var product = await session.LoadAsync<UcpCatalogItem>(line.ProductId, ct);
            if (product is null || !product.Available)
            {
                messages.Add(new MessageItem { Code = UcpResourceConstants.RECORD_NOT_FOUND });
                continue;
            }

            var unitMinor = (long)Math.Round(product.Price * 100m, MidpointRounding.AwayFromZero);
            var created = UcpLineItem.Create(line.ProductId, product.Title, unitMinor, line.Quantity);
            if (!created.IsSuccess)
            {
                messages.AddRange(created.Messages ?? []);
                continue;
            }
            items.Add(created.Data!);
        }

        return new Resolved(items, messages);
    }
}