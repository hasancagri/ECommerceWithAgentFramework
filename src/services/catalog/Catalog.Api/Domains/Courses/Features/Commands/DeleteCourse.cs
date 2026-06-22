
namespace Catalog.Api.Domains.Courses.Features.Commands;

public static class DeleteCourse
{
    public record DeleteCourseCommand(Guid Id);

    [Transactional]
    public class DeleteCourseHandler(IDocumentSession session)
    {
        public async Task Handle(DeleteCourseCommand cmd, CancellationToken ct)
        {
            var course = await session.LoadAsync<Course>(cmd.Id, ct);
            if (course is null) return;

            course.Delete();
            session.Store(course);
        }
    }
}

public static class DeleteCourseEndpoint
{
    public static RouteGroupBuilder DeleteCourseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                await bus.InvokeAsync(new DeleteCourse.DeleteCourseCommand(id));
                return Results.Ok();
            })
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}