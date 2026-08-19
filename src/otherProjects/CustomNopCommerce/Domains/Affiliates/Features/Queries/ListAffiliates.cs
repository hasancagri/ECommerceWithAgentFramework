namespace CustomNopCommerce.Domains.Affiliates.Features.Queries;

/// <summary>Satıcı-ortaklarını listeleyen read-slice'ı.</summary>
public static class ListAffiliates
{
    public record ListAffiliatesQuery;

    public class AffiliateItem
    {
        public Guid Id { get; set; }
        public string FriendlyUrlName { get; set; } = default!;
        public bool IsActive { get; set; }
    }

    public class ListAffiliatesQueryHandler
    {
        public async Task<FeatureListResultModel<AffiliateItem>> Handle(
            ListAffiliatesQuery query, IQuerySession session, CancellationToken ct)
        {
            var affiliates = await session.Query<Affiliate>()
                .Where(a => !a.IsDeleted)
                .ToListAsync(ct);

            var items = affiliates.Select(a => new AffiliateItem
            {
                Id = a.Id,
                FriendlyUrlName = a.FriendlyUrlName,
                IsActive = a.IsActive,
            }).ToList();

            return FeatureListResultModel<AffiliateItem>.Ok(items);
        }
    }
}

public static class ListAffiliatesQueryEndpoint
{
    public static RouteGroupBuilder ListAffiliatesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListAffiliates.AffiliateItem>>(
                    new ListAffiliates.ListAffiliatesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListAffiliates");
        return group;
    }
}
