namespace CustomNopCommerce.Domains.UrlRecords.Features.Queries;

/// <summary>Bir varlığın tüm slug'larını (aktif + eski/redirect) listeleyen read-slice'ı.</summary>
public static class ListSlugHistory
{
    public record ListSlugHistoryQuery(string EntityName, Guid EntityId);

    public class SlugItem
    {
        public Guid Id { get; set; }
        public string Slug { get; set; } = default!;
        public bool IsActive { get; set; }
    }

    public class ListSlugHistoryQueryHandler
    {
        public async Task<FeatureListResultModel<SlugItem>> Handle(
            ListSlugHistoryQuery query, IQuerySession session, CancellationToken ct)
        {
            var records = await session.Query<UrlRecord>()
                .Where(u => u.EntityName == query.EntityName && u.EntityId == query.EntityId && !u.IsDeleted)
                .ToListAsync(ct);

            var items = records.Select(u => new SlugItem
            {
                Id = u.Id,
                Slug = u.Slug,
                IsActive = u.IsActive,
            }).ToList();

            return FeatureListResultModel<SlugItem>.Ok(items);
        }
    }
}

public static class ListSlugHistoryQueryEndpoint
{
    public static RouteGroupBuilder ListSlugHistoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/history/{entityName}/{entityId:guid}", async (string entityName, Guid entityId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListSlugHistory.SlugItem>>(
                    new ListSlugHistory.ListSlugHistoryQuery(entityName, entityId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListSlugHistory");
        return group;
    }
}
