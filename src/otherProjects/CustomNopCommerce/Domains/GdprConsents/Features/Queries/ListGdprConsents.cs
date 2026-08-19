namespace CustomNopCommerce.Domains.GdprConsents.Features.Queries;

/// <summary>GDPR rıza tanımlarını sıralı listeleyen read-slice'ı.</summary>
public static class ListGdprConsents
{
    public record ListGdprConsentsQuery;

    public class ConsentItem
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = default!;
        public bool IsRequired { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ListGdprConsentsQueryHandler
    {
        public async Task<FeatureListResultModel<ConsentItem>> Handle(
            ListGdprConsentsQuery query, IQuerySession session, CancellationToken ct)
        {
            var consents = await session.Query<GdprConsent>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var items = consents
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new ConsentItem
                {
                    Id = c.Id,
                    Message = c.Message,
                    IsRequired = c.IsRequired,
                    DisplayOrder = c.DisplayOrder,
                }).ToList();

            return FeatureListResultModel<ConsentItem>.Ok(items);
        }
    }
}

public static class ListGdprConsentsQueryEndpoint
{
    public static RouteGroupBuilder ListGdprConsentsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListGdprConsents.ConsentItem>>(
                    new ListGdprConsents.ListGdprConsentsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListGdprConsents");
        return group;
    }
}
