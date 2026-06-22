using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Services;
using WebApp.ViewModel;


namespace WebApp.Pages;

public class IndexModel(CatalogService catalogService, ILogger<IndexModel> logger) : BasePageModel
{
    public List<CourseViewModel>? Courses { get; set; } = [];
    public async Task<IActionResult> OnGet()
    {
        var coursesAsResult = await catalogService.GetAllCoursesAsync();

        if (coursesAsResult.IsFail) return ErrorPage(coursesAsResult);
        Courses = coursesAsResult.Data!;
        return Page();
    }
}