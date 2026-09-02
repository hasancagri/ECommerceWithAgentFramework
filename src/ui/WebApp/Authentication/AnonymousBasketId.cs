namespace WebApp.Authentication;

// 057: girissiz ziyaretcinin sepet kimligi — HttpOnly kalici cookie'de rastgele Guid.
// Basket.Api bu Guid'i sepet sahibi olarak kullanir; login'de merge sonrasi cookie silinir.
public static class AnonymousBasketId
{
    public const string CookieName = "AnonymousBasketId";
    public const string HeaderName = "X-Anonymous-Id";

    // Cookie varsa dondurur; yoksa uretip response'a yazar (ayni istek icinde kullanilir).
    public static Guid GetOrCreate(HttpContext httpContext)
    {
        if (Guid.TryParse(httpContext.Request.Cookies[CookieName], out var existing))
            return existing;

        var id = Guid.NewGuid();

        // Response basladiysa cookie yazilamaz; Guid yine kullanilir, kalicilik bir sonraki istege kalir.
        if (!httpContext.Response.HasStarted)
            httpContext.Response.Cookies.Append(CookieName, id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            });

        return id;
    }

    public static Guid? Get(HttpContext httpContext) =>
        Guid.TryParse(httpContext.Request.Cookies[CookieName], out var id) ? id : null;

    public static void Clear(HttpContext httpContext) =>
        httpContext.Response.Cookies.Delete(CookieName);
}