
namespace Catalog.Api.Domains.Courses.Features.Commands;

public static class CreateCourse
{
    public record CreateCourseCommand(
        string Name,
        string Description,
        decimal Price,
        Guid UserId,
        Guid CategoryId,
        string? ImageUrl,
        int Duration,
        string EducatorFullName);

    public class CreateCourseResponse
    {
        public Guid Id { get; set; }
    }
    
    [Transactional]
    public class CreateCourseCommandHandler(IDocumentSession session)
    {
        public async Task<FeatureObjectResultModel<CreateCourseResponse>> Handle(CreateCourseCommand cmd, CancellationToken ct)
        {
            var feature = new Feature(cmd.Duration, 0f, cmd.EducatorFullName);
            var course = Course.Create(cmd.Name, cmd.Description, cmd.Price, cmd.UserId, cmd.CategoryId, cmd.ImageUrl, feature);
            session.Store(course);
            return FeatureObjectResultModel<CreateCourseResponse>.Ok(new CreateCourseResponse { Id = course.Id });
        }
    }
}

public static class CreateCourseCommandEndpoint
{
    public static RouteGroupBuilder CreateCourseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateCourse.CreateCourseCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateCourse.CreateCourseResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}