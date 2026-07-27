namespace Catalog.Api.Domains.Categories.Features.Queries;

public static class GetAllCategories
{
    [Cached("catalog-products", 60)]
    public record GetAllCategoriesQuery();

    public class CategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class GetAllCategoriesQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<CategoryResponse>>> Handle(
            GetAllCategoriesQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var categories = await session.Query<Category>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(x => new CategoryResponse { Id = x.Id, Name = x.Name })
                .ToListAsync(ct);

            return FeatureObjectResultModel<List<CategoryResponse>>.Ok(categories.ToList());
        }
    }
}

public static class GetAllCategoriesQueryEndpoint
{
    public static RouteGroupBuilder GetAllCategoriesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllCategories.CategoryResponse>>>(
                    new GetAllCategories.GetAllCategoriesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetAllCategories")
            .AllowAnonymous();
        return group;
    }
}