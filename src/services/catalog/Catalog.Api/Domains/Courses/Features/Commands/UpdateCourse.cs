
namespace Catalog.Api.Domains.Courses.Features.Commands;

public static class UpdateCourse
{
    public record UpdateCourseCommand(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string? ImageUrl,
        Guid CategoryId,
        int Duration,
        string EducatorFullName);
    

    [Transactional]
    public class UpdateCourseCommandHandler(IDocumentSession session)
    {
        public async Task Handle(UpdateCourseCommand cmd, CancellationToken ct)
        {
            var course = await session.LoadAsync<Course>(cmd.Id, ct);
            if (course is null) return;

            var updatedFeature = new Feature(cmd.Duration, course.Feature.Rating, cmd.EducatorFullName);
            course.Update(cmd.Name, cmd.Description, cmd.Price, cmd.ImageUrl, cmd.CategoryId, updatedFeature);
            session.Store(course);
        }
    }
}

public static class UpdateCourseCommandEndpoint
{
    public static RouteGroupBuilder UpdateCourseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/", async ([FromBody] UpdateCourse.UpdateCourseCommand cmd, IMessageBus bus) =>
            {
                await bus.InvokeAsync(cmd);
                return Results.Ok();
            })
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}