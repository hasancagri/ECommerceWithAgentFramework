

namespace Catalog.Api.Domains.Courses.Features.Queries;

public static class GetCourseById
{
    public record GetCourseByIdQuery(Guid Id);

    public record GetCourseByIdResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public decimal Price { get; init; }
        public string? ImageUrl { get; init; }
        public DateTime Created { get; init; }
        public CategoryInfo Category { get; init; }
        public FeatureInfo Feature { get; init; }

        public record CategoryInfo(Guid Id, string Name);
        public record FeatureInfo(int Duration, float Rating, string EducatorFullName);

        public static GetCourseByIdResponse From(Course course, Category category) => new()
        {
            Id = course.Id,
            Name = course.Name,
            Description = course.Description,
            Price = course.Price,
            ImageUrl = course.ImageUrl,
            Created = course.CreatedTime,
            Category = new CategoryInfo(category.Id, category.Name),
            Feature = new FeatureInfo(course.Feature.Duration, course.Feature.Rating, course.Feature.EducatorFullName)
        };
    }

    public class GetCourseByIdQueryHandler(IQuerySession session)
    {

        public async Task<FeatureObjectResultModel<GetCourseByIdResponse>> Handle(
            GetCourseByIdQuery query, CancellationToken ct)
        {
            var course = await session.LoadAsync<Course>(query.Id, ct);
            if (course is null)
                return FeatureObjectResultModel<GetCourseByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Course),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            var category = await session.LoadAsync<Category>(course.CategoryId, ct);
            if (category is null)
                return FeatureObjectResultModel<GetCourseByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Category),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetCourseByIdResponse>.Ok(GetCourseByIdResponse.From(course, category));
        }
    }
}

public static class GetCourseByIdEndpoint
{
    public static RouteGroupBuilder GetByIdCourseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetCourseById.GetCourseByIdResponse>>(
                    new GetCourseById.GetCourseByIdQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}