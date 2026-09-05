namespace Identity.Server.Pages.Consent;

// 061: Dış agent (Explicit consent) istemcileri için tek onay sayfası (R6/FR-005).
// Onayla → kalıcı OpenIddict authorization yazılır (karar SUNUCUDA doğar; istemci URL
// parametresiyle onay üretemez), authorize akışı kaldığı yerden tamamlanır.
// Reddet → authorize'a consent=denied ile dönülür; istemci access_denied alır, yan etki yok.
[SecurityHeaders]
public class Index(
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    RoleScopeQuery roleScopeQuery) : PageModel
{
    [BindProperty]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? Button { get; set; }

    public string ClientName { get; private set; } = "";
    public IReadOnlyList<string> Scopes { get; private set; } = [];

    public async Task<IActionResult> OnGet(string? returnUrl)
    {
        if (!TryParseAuthorizeReturnUrl(returnUrl, out var clientId, out var requestedScopes))
            return BadRequest();

        var application = await applicationManager.FindByClientIdAsync(clientId, HttpContext.RequestAborted);
        if (application is null)
            return BadRequest();

        ClientName = await applicationManager.GetDisplayNameAsync(application, HttpContext.RequestAborted)
                     ?? clientId;
        Scopes = await ResolveGrantedScopesAsync(requestedScopes);
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!TryParseAuthorizeReturnUrl(ReturnUrl, out var clientId, out var requestedScopes))
            return BadRequest();

        if (Button != "accept")
        {
            // Reddet: authorize ucu consent=denied görüp standart access_denied döner (SC-005).
            var separator = ReturnUrl!.Contains('?') ? '&' : '?';
            return Redirect($"{ReturnUrl}{separator}consent=denied");
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, HttpContext.RequestAborted);
        if (application is null)
            return BadRequest();

        var subject = userManager.GetUserId(User)
            ?? throw new InvalidOperationException("Oturum kullanıcısı çözülemedi.");

        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = await applicationManager.GetIdAsync(application, HttpContext.RequestAborted),
            Subject = subject,
            Status = Statuses.Valid,
            Type = AuthorizationTypes.Permanent,
        };
        descriptor.Scopes.UnionWith(await ResolveGrantedScopesAsync(requestedScopes));

        await authorizationManager.CreateAsync(descriptor, HttpContext.RequestAborted);

        return Redirect(ReturnUrl!);
    }

    // Onaylanan scope'lar authorize'daki granted kümeyle AYNI formülle üretilir:
    // requested ∩ (rol demeti ∪ kimlik scope'ları) — yoksa FindAsync eşleşmez, consent döngüye girer.
    private async Task<IReadOnlyList<string>> ResolveGrantedScopesAsync(IReadOnlyList<string> requested)
    {
        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var roleBundle = await roleScopeQuery.GetUserScopeBundleAsync(user, HttpContext.RequestAborted);
        return ScopeResolver.Resolve(requested, roleBundle, Config.IdentityScopes.ToHashSet());
    }

    // returnUrl yalnız yerel /connect/authorize olabilir; client_id + scope oradan okunur.
    private static bool TryParseAuthorizeReturnUrl(
        string? returnUrl, out string clientId, out IReadOnlyList<string> requestedScopes)
    {
        clientId = "";
        requestedScopes = [];

        if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//"))
            return false;

        var queryIndex = returnUrl.IndexOf('?');
        if (queryIndex < 0 || !returnUrl[..queryIndex].Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase))
            return false;

        var query = QueryHelpers.ParseQuery(returnUrl[queryIndex..]);
        if (!query.TryGetValue("client_id", out var clientIdValue) || string.IsNullOrEmpty(clientIdValue))
            return false;

        clientId = clientIdValue.ToString();
        requestedScopes = query.TryGetValue("scope", out var scopeValue)
            ? scopeValue.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [];
        return true;
    }
}