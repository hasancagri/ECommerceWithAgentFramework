namespace WebApp.Pages.Basket.Dto;

public record AddBasketRequest(
    Guid CourseId,
    string CourseName,
    decimal CoursePrice,
    string? ImageUrl
);