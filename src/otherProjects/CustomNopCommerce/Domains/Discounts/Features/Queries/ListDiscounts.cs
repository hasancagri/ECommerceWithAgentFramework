namespace CustomNopCommerce.Domains.Discounts.Features.Queries;

/// <summary>İndirimleri (kullanım sayısıyla) listeleyen read-slice'ı.</summary>
public static class ListDiscounts
{
    public record ListDiscountsQuery;

    public class DiscountItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public DiscountType Type { get; set; }
        public bool IsActive { get; set; }
        public bool RequiresCouponCode { get; set; }
        public int UsageCount { get; set; }
    }

    public class ListDiscountsQueryHandler
    {
        public async Task<FeatureListResultModel<DiscountItem>> Handle(
            ListDiscountsQuery query, IQuerySession session, CancellationToken ct)
        {
            var discounts = await session.Query<Discount>()
                .Where(d => !d.IsDeleted)
                .ToListAsync(ct);

            var items = discounts.Select(d => new DiscountItem
            {
                Id = d.Id,
                Name = d.Name,
                Type = d.Type,
                IsActive = d.IsActive,
                RequiresCouponCode = d.RequiresCouponCode,
                UsageCount = d.Usages.Count,
            }).ToList();

            return FeatureListResultModel<DiscountItem>.Ok(items);
        }
    }
}

public static class ListDiscountsQueryEndpoint
{
    public static RouteGroupBuilder ListDiscountsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListDiscounts.DiscountItem>>(
                    new ListDiscounts.ListDiscountsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListDiscounts");
        return group;
    }
}
