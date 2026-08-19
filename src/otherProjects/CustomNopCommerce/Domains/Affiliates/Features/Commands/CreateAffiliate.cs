namespace CustomNopCommerce.Domains.Affiliates.Features.Commands;

/// <summary>Yeni satıcı-ortağı oluşturma write-slice'ı. Referral slug tekliği burada (query) korunur.</summary>
public static class CreateAffiliate
{
    public record CreateAffiliateCommand(string FriendlyUrlName, Guid? AddressId, string? AdminComment);

    public class CreateAffiliateResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateAffiliateCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateAffiliateResponse>> Handle(
            CreateAffiliateCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.FriendlyUrlName))
                return FeatureObjectResultModel<CreateAffiliateResponse>.Error(new MessageItem
                { Property = nameof(cmd.FriendlyUrlName), Code = AffiliatesResourceConstants.URL_REQUIRED });

            var normalized = cmd.FriendlyUrlName.Trim().ToLowerInvariant().Replace(' ', '-');

            var taken = await session.Query<Affiliate>()
                .Where(a => a.FriendlyUrlName == normalized && !a.IsDeleted)
                .AnyAsync(ct);
            if (taken)
                return FeatureObjectResultModel<CreateAffiliateResponse>.Error(new MessageItem
                { Property = nameof(cmd.FriendlyUrlName), Code = AffiliatesResourceConstants.URL_TAKEN });

            var affiliate = Affiliate.Create(normalized, cmd.AddressId, cmd.AdminComment);
            session.Store(affiliate);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateAffiliateResponse>.Ok(
                new CreateAffiliateResponse { Id = affiliate.Id });
        }
    }
}

public static class CreateAffiliateCommandEndpoint
{
    public static RouteGroupBuilder CreateAffiliateGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateAffiliate.CreateAffiliateCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateAffiliate.CreateAffiliateResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateAffiliate");
        return group;
    }
}
