namespace WebApp.Pages.Auth;

// Login artik Identity.Server'da (OIDC). Bu sayfa sadece challenge/sign-out tetikler.
public class SignInModel : PageModel
{
    // GET /Auth/SignIn → Identity.Server login sayfasina yonlendir (OIDC challenge).
    public IActionResult OnGet(string? returnUrl = null)
        => Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
            OpenIdConnectDefaults.AuthenticationScheme);

    // GET /Auth/SignIn?handler=SignOut → hem cookie hem Identity.Server oturumunu kapat.
    public IActionResult OnGetSignOut()
        => SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
}