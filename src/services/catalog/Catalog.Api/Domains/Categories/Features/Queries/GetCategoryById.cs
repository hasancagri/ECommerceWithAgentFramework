

namespace Catalog.Api.Domains.Categories.Features.Queries;

public static class GetCategoryById
{
    public record GetCategoryByIdQuery(Guid Id);

    public class GetCategoryByIdResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = default!;

        public static GetCategoryByIdResponse From(Category category) => new()
        {
            Id = category.Id,
            Name = category.Name
        };
    }

    public class GetCategoryByIdQueryHandler(IQuerySession session)
    {

        public async Task<FeatureObjectResultModel<GetCategoryByIdResponse>> Handle(
            GetCategoryByIdQuery query, CancellationToken ct)
        {
            var category = await session.LoadAsync<Category>(query.Id, ct);
            if (category is null)
                return FeatureObjectResultModel<GetCategoryByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Category),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetCategoryByIdResponse>.Ok(GetCategoryByIdResponse.From(category));
        }
    }
}

public static class GetCategoryByIdEndpoint
{
    public static RouteGroupBuilder GetByIdCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetCategoryById.GetCategoryByIdResponse>>(
                new GetCategoryById.GetCategoryByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        })
        .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}