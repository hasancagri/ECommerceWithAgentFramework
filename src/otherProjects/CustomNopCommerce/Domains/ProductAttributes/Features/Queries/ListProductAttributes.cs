namespace CustomNopCommerce.Domains.ProductAttributes.Features.Queries;

/// <summary>Global öznitelikleri (değer şablonu sayısıyla) listeleyen read-slice'ı.</summary>
public static class ListProductAttributes
{
    public record ListProductAttributesQuery;

    public class ListProductAttributesItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int PredefinedValueCount { get; set; }
    }

    public class ListProductAttributesQueryHandler
    {
        public async Task<FeatureListResultModel<ListProductAttributesItem>> Handle(
            ListProductAttributesQuery query, IQuerySession session, CancellationToken ct)
        {
            var attributes = await session.Query<ProductAttribute>()
                .Where(a => !a.IsDeleted)
                .ToListAsync(ct);

            var items = attributes.Select(a => new ListProductAttributesItem
            {
                Id = a.Id,
                Name = a.Name,
                PredefinedValueCount = a.PredefinedValues.Count,
            }).ToList();

            return FeatureListResultModel<ListProductAttributesItem>.Ok(items);
        }
    }
}

public static class ListProductAttributesQueryEndpoint
{
    public static RouteGroupBuilder ListProductAttributesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListProductAttributes.ListProductAttributesItem>>(
                    new ListProductAttributes.ListProductAttributesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListProductAttributes");
        return group;
    }
}
