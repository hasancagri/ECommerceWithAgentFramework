using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApp.Services;
using WebApp.ViewModel;


namespace WebApp.Pages.Products;

[Authorize]
public class CreateProductModel(CatalogService catalogService) : PageModel
{
    [BindProperty] public CreateProductViewModel ViewModel { get; set; } = CreateProductViewModel.Empty;

    public void OnGet()
    {
        ViewModel.SetBrandDropdownList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await catalogService.CreateProductAsync(ViewModel);

        if (!result.IsSuccess)
        {
            ViewModel.SetBrandDropdownList();
            return Page();
        }

        return RedirectToPage("/Index");
    }
}