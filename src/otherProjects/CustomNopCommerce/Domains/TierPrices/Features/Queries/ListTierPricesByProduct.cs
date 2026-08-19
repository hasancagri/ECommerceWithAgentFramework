namespace CustomNopCommerce.Domains.TierPrices.Features.Queries;

/// <summary>Bir ürünün kademeli fiyatlarını (adet eşiğine göre sıralı) listeleyen read-slice'ı.</summary>
public static class ListTierPricesByProduct
{
    public record ListTierPricesByProductQuery(Guid ProductId);

    public class TierPriceItem
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public Guid? CustomerRoleId { get; set; }
    }

    public class ListTierPricesByProductQueryHandler
    {
        public async Task<FeatureListResultModel<TierPriceItem>> Handle(
            ListTierPricesByProductQuery query, IQuerySession session, CancellationToken ct)
        {
            var tierPrices = await session.Query<TierPrice>()
                .Where(t => t.ProductId == query.ProductId && !t.IsDeleted)
                .ToListAsync(ct);

            var items = tierPrices
                .OrderBy(t => t.Quantity)
                .Select(t => new TierPriceItem
                {
                    Id = t.Id,
                    Quantity = t.Quantity,
                    Price = t.Price,
                    CustomerRoleId = t.CustomerRoleId,
                }).ToList();

            return FeatureListResultModel<TierPriceItem>.Ok(items);
        }
    }
}

public static class ListTierPricesByProductQueryEndpoint
{
    public static RouteGroupBuilder ListTierPricesByProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-product/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListTierPricesByProduct.TierPriceItem>>(
                    new ListTierPricesByProduct.ListTierPricesByProductQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListTierPricesByProduct");
        return group;
    }
}
