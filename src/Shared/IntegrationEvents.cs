namespace Shared;

public static class IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalPrice);
    public record UploadCoursePictureCommand(Guid CourseId, string FileName, byte[] Picture);
    public record CoursePictureUploadedEvent(Guid CourseId, string ImageUrl);
}