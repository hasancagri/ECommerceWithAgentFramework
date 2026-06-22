#region

using WebApp.Pages.Instructor.Dto;

#endregion

namespace WebApp.Dto;

public record CourseDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    DateTime Created,
    CategoryDto Category,
    FeatureDto Feature);