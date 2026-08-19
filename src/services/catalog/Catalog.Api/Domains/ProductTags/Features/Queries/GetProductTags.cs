namespace Catalog.Api.Domains.ProductTags.Features.Queries;

public static class GetProductTags
{
    public record GetProductTagsQuery;

    public class GetProductTagsResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class GetProductTagsQueryHandler
    {
        public async Task<FeatureListResultModel<GetProductTagsResponse>> Handle(
            GetProductTagsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var tags = await session.Query<ProductTag>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            return FeatureListResultModel<GetProductTagsResponse>.Ok(
                tags.Select(x => new GetProductTagsResponse { Id = x.Id, Name = x.Name }).ToList());
        }
    }
}

public static class GetProductTagsQueryEndpoint
{
    public static RouteGroupBuilder GetProductTagsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetProductTags.GetProductTagsResponse>>(
                    new GetProductTags.GetProductTagsQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetProductTags");
        return group;
    }
}