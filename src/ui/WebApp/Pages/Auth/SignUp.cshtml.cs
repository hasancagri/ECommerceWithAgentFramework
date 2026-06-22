using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApp.Pages.Auth;

// Kayit OIDC flow'u icinde baslar (prompt=create). Identity.Server kayit sayfasini
// gosterir; kayit bitince flow WebApp'e geri doner ve kullanici login olmus olarak
// RedirectUri'ye (dashboard) duser -- 5001 home'a degil.
public class SignUpModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        var props = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
        props.Items["prompt"] = "create";
        return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
    }
}