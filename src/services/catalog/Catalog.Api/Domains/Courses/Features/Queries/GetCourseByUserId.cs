

namespace Catalog.Api.Domains.Courses.Features.Queries;

public static class GetCourseByUserId
{
    public record GetCourseByUserIdQuery(Guid UserId);

    public record GetCourseByUserIdResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public decimal Price { get; init; }
        public Guid UserId { get; init; }
        public string? ImageUrl { get; init; }
        public Guid CategoryId { get; init; }
        public int Duration { get; init; }
        public float Rating { get; init; }
        public string EducatorFullName { get; init; }

        public static GetCourseByUserIdResponse From(Course course) => new()
        {
            Id = course.Id,
            Name = course.Name,
            Description = course.Description,
            Price = course.Price,
            UserId = course.UserId,
            ImageUrl = course.ImageUrl,
            CategoryId = course.CategoryId,
            Duration = course.Feature.Duration,
            Rating = course.Feature.Rating,
            EducatorFullName = course.Feature.EducatorFullName
        };
    }

    public class GetCourseByUserIdQueryHandler(IQuerySession session)
    {

        public async Task<FeatureObjectResultModel<List<GetCourseById.GetCourseByIdResponse>>> Handle(
            GetCourseByUserIdQuery query, CancellationToken ct)
        {
            var courses = await session.Query<Course>()
                .Where(x => x.UserId == query.UserId && !x.IsDeleted)
                .ToListAsync(ct);

            var categoryIds = courses.Select(c => c.CategoryId).Distinct().ToList();
            var categories = await session.Query<Category>()
                .Where(x => categoryIds.Contains(x.Id))
                .ToListAsync(ct);
            var categoryDict = categories.ToDictionary(c => c.Id);

            var response = courses
                .Where(c => categoryDict.ContainsKey(c.CategoryId))
                .Select(c => GetCourseById.GetCourseByIdResponse.From(c, categoryDict[c.CategoryId]))
                .ToList();

            return FeatureObjectResultModel<List<GetCourseById.GetCourseByIdResponse>>.Ok(response);
        }
    }
}

public static class GetCourseByUserIdEndpoint
{
    public static RouteGroupBuilder GetByUserIdCourseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/user/{userId:guid}", async (Guid userId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetCourseById.GetCourseByIdResponse>>>(
                    new GetCourseByUserId.GetCourseByUserIdQuery(userId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}