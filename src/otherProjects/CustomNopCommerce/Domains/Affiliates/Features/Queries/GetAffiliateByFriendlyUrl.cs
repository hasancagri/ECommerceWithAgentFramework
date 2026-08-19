namespace CustomNopCommerce.Domains.Affiliates.Features.Queries;

/// <summary>Referral slug'ını aktif bir ortağa çözen read-slice'ı. Ziyaretçi ?affiliate=slug ile gelince
/// siparişi hangi ortağa atfedeceğini bulmak için kullanılır.</summary>
public static class GetAffiliateByFriendlyUrl
{
    public record GetAffiliateByFriendlyUrlQuery(string FriendlyUrlName);

    public class AffiliateResponse
    {
        public Guid Id { get; set; }
        public string FriendlyUrlName { get; set; } = default!;
    }

    public class GetAffiliateByFriendlyUrlQueryHandler
    {
        public async Task<FeatureObjectResultModel<AffiliateResponse>> Handle(
            GetAffiliateByFriendlyUrlQuery query, IQuerySession session, CancellationToken ct)
        {
            var normalized = query.FriendlyUrlName.Trim().ToLowerInvariant().Replace(' ', '-');
            var affiliate = await session.Query<Affiliate>()
                .Where(a => a.FriendlyUrlName == normalized && a.IsActive && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (affiliate is null)
                return FeatureObjectResultModel<AffiliateResponse>.NotFound();

            return FeatureObjectResultModel<AffiliateResponse>.Ok(new AffiliateResponse
            {
                Id = affiliate.Id,
                FriendlyUrlName = affiliate.FriendlyUrlName,
            });
        }
    }
}

public static class GetAffiliateByFriendlyUrlQueryEndpoint
{
    public static RouteGroupBuilder GetAffiliateByFriendlyUrlGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/resolve/{friendlyUrl}", async (string friendlyUrl, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetAffiliateByFriendlyUrl.AffiliateResponse>>(
                    new GetAffiliateByFriendlyUrl.GetAffiliateByFriendlyUrlQuery(friendlyUrl));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetAffiliateByFriendlyUrl");
        return group;
    }
}
