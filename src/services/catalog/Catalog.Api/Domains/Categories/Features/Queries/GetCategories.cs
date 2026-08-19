namespace Catalog.Api.Domains.Categories.Features.Queries;

public static class GetCategories
{
    public record GetCategoriesQuery;

    public class GetCategoriesResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public Guid? ParentCategoryId { get; set; }
        public int DisplayOrder { get; set; }
        public bool Published { get; set; }
    }

    public class GetCategoriesQueryHandler
    {
        public async Task<FeatureListResultModel<GetCategoriesResponse>> Handle(
            GetCategoriesQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var categories = await session.Query<Category>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .ToListAsync(ct);

            return FeatureListResultModel<GetCategoriesResponse>.Ok(
                categories.Select(x => new GetCategoriesResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    ParentCategoryId = x.ParentCategoryId,
                    DisplayOrder = x.DisplayOrder,
                    Published = x.Published
                }).ToList());
        }
    }
}

public static class GetCategoriesQueryEndpoint
{
    public static RouteGroupBuilder GetCategoriesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetCategories.GetCategoriesResponse>>(
                    new GetCategories.GetCategoriesQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetCategories");
        return group;
    }
}