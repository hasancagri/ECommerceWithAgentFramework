namespace CustomNopCommerce.Domains.SpecificationAttributes.Features.Queries;

/// <summary>Spesifikasyonları (seçenek sayısıyla) listeleyen read-slice'ı.</summary>
public static class ListSpecificationAttributes
{
    public record ListSpecificationAttributesQuery;

    public class SpecItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public Guid? GroupId { get; set; }
        public int OptionCount { get; set; }
    }

    public class ListSpecificationAttributesQueryHandler
    {
        public async Task<FeatureListResultModel<SpecItem>> Handle(
            ListSpecificationAttributesQuery query, IQuerySession session, CancellationToken ct)
        {
            var specs = await session.Query<SpecificationAttribute>()
                .Where(s => !s.IsDeleted)
                .ToListAsync(ct);

            var items = specs
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new SpecItem
                {
                    Id = s.Id,
                    Name = s.Name,
                    GroupId = s.GroupId,
                    OptionCount = s.Options.Count,
                }).ToList();

            return FeatureListResultModel<SpecItem>.Ok(items);
        }
    }
}

public static class ListSpecificationAttributesQueryEndpoint
{
    public static RouteGroupBuilder ListSpecificationAttributesItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListSpecificationAttributes.SpecItem>>(
                    new ListSpecificationAttributes.ListSpecificationAttributesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListSpecificationAttributes");
        return group;
    }
}
