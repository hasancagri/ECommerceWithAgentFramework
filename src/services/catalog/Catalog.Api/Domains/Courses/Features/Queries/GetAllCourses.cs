
namespace Catalog.Api.Domains.Courses.Features.Queries;

public static class GetAllCourses
{
    public record GetAllCoursesQuery;

    public class GetAllCoursesQueryHandler(IQuerySession session)
    {
       
        public async Task<FeatureObjectResultModel<List<GetCourseById.GetCourseByIdResponse>>> Handle(
            GetAllCoursesQuery query, CancellationToken ct)
        {
            var courses = await session.Query<Course>()
                .Where(x => !x.IsDeleted)
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

public static class GetAllCoursesEndpoint
{
    public static RouteGroupBuilder GetAllCourseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetCourseById.GetCourseByIdResponse>>>(
                new GetAllCourses.GetAllCoursesQuery());
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        })
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}