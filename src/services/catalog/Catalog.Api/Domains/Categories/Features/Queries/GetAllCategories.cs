

namespace Catalog.Api.Domains.Categories.Features.Queries;

public static class GetAllCategories
{
    public record GetAllCategoriesQuery;

    public class GetAllCategoriesQueryHandler(IQuerySession session)
    {

        public async Task<FeatureObjectResultModel<List<GetCategoryById.GetCategoryByIdResponse>>> Handle(
            GetAllCategoriesQuery query, CancellationToken ct)
        {
            var categories = await session.Query<Category>()
                .Where(x => !x.IsDeleted)
                .ToListAsync(ct);

            var response = categories
                .Select(GetCategoryById.GetCategoryByIdResponse.From)
                .ToList();

            return FeatureObjectResultModel<List<GetCategoryById.GetCategoryByIdResponse>>.Ok(response);
        }
    }
}

public static class GetAllCategoriesEndpoint
{
    public static RouteGroupBuilder GetAllCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetCategoryById.GetCategoryByIdResponse>>>(
                new GetAllCategories.GetAllCategoriesQuery());
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        })
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}