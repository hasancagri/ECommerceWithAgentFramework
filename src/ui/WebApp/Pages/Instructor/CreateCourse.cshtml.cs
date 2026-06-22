using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApp.Services;
using WebApp.ViewModel;


namespace WebApp.Pages.Instructor;

[Authorize(Roles = "instructor")]
public class CreateCourseModel(CatalogService catalogService) : PageModel
{
    [BindProperty] public CreateCourseViewModel ViewModel { get; set; } = CreateCourseViewModel.Empty;

    public async Task OnGetAsync()
    {
        var categoriesResult = await catalogService.GetCategoriesAsync();


        if (categoriesResult.IsFail)
        {
            //TODO : redirect error page
        }

        ViewModel.SetCategoryDropdownList(categoriesResult.Data!);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await catalogService.CreateCourseAsync(ViewModel);

        if (!result.IsSuccess)
        {
            //TODO : Show error
        }

        return RedirectToPage("Courses");
    }
}