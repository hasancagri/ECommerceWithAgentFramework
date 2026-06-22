
namespace Catalog.Api;

public static class EventHandlers
{
    public static async Task Handle(IntegrationEvents.CoursePictureUploadedEvent evt, IDocumentSession session,
        CancellationToken ct)
    {
        var course = await session.LoadAsync<Course>(evt.CourseId, ct);
        if (course is null) return;

        course.UpdateImageUrl(evt.ImageUrl);
        session.Store(course);
        await session.SaveChangesAsync(ct);
    }
}