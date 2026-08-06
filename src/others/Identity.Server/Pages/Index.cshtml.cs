using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Identity.Server.Pages.Home;

[AllowAnonymous]
public class Index : PageModel
{
    public void OnGet()
    {
    }
}