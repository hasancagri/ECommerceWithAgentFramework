namespace CustomNopCommerce.Domains.ProductTags.Features.Queries;

/// <summary>Ürün etiketlerini listeleyen read-slice'ı.</summary>
public static class ListProductTags
{
    public record ListProductTagsQuery;

    public class ListProductTagsItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class ListProductTagsQueryHandler
    {
        public async Task<FeatureListResultModel<ListProductTagsItem>> Handle(
            ListProductTagsQuery query, IQuerySession session, CancellationToken ct)
        {
            var tags = await session.Query<ProductTag>()
                .Where(t => !t.IsDeleted)
                .ToListAsync(ct);

            var items = tags.Select(t => new ListProductTagsItem { Id = t.Id, Name = t.Name }).ToList();
            return FeatureListResultModel<ListProductTagsItem>.Ok(items);
        }
    }
}

public static class ListProductTagsQueryEndpoint
{
    public static RouteGroupBuilder ListProductTagsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListProductTags.ListProductTagsItem>>(
                    new ListProductTags.ListProductTagsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListProductTags");
        return group;
    }
}
