namespace CustomNopCommerce.Domains.SpecificationAttributeGroups.Features.Queries;

/// <summary>Spesifikasyon gruplarını sıralı listeleyen read-slice'ı.</summary>
public static class ListSpecificationAttributeGroups
{
    public record ListSpecificationAttributeGroupsQuery;

    public class GroupItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
    }

    public class ListSpecificationAttributeGroupsQueryHandler
    {
        public async Task<FeatureListResultModel<GroupItem>> Handle(
            ListSpecificationAttributeGroupsQuery query, IQuerySession session, CancellationToken ct)
        {
            var groups = await session.Query<SpecificationAttributeGroup>()
                .Where(g => !g.IsDeleted)
                .ToListAsync(ct);

            var items = groups
                .OrderBy(g => g.DisplayOrder)
                .Select(g => new GroupItem { Id = g.Id, Name = g.Name, DisplayOrder = g.DisplayOrder })
                .ToList();

            return FeatureListResultModel<GroupItem>.Ok(items);
        }
    }
}

public static class ListSpecificationAttributeGroupsQueryEndpoint
{
    public static RouteGroupBuilder ListSpecificationAttributeGroupsItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListSpecificationAttributeGroups.GroupItem>>(
                    new ListSpecificationAttributeGroups.ListSpecificationAttributeGroupsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListSpecificationAttributeGroups");
        return group;
    }
}
