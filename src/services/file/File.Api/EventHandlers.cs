
namespace File.Api;

public static class EventHandlers
{
    public static async Task<IntegrationEvents.CoursePictureUploadedEvent> Handle(
        IntegrationEvents.UploadCoursePictureCommand cmd,
        IFileProvider fileProvider)
    {
        var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(cmd.FileName)}";
        var uploadPath = Path.Combine(fileProvider.GetFileInfo("files").PhysicalPath!, newFileName);

        await System.IO.File.WriteAllBytesAsync(uploadPath, cmd.Picture);

        return new IntegrationEvents.CoursePictureUploadedEvent(cmd.CourseId, $"files/{newFileName}");
    }
}