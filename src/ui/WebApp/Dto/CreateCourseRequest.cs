namespace WebApp.Dto;

public record CreateCourseRequest(
    string Name,
    string Description,
    decimal Price,
    IFormFile? Picture,
    Guid CategoryId);