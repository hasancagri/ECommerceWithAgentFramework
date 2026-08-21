using System.Security.Claims;

namespace WebApp.Services.Behavior;

// 042: davranış kimlik çerezleri (R8). pz_aid = kalıcı anonim kimlik (1 yıl), pz_sid = oturum
// kimliği (tarayıcı kapanınca ölür). İkisi de rastgele GUID — kişisel veri taşımaz (FR-007).
// İlk istekte üretilen değer aynı isteğin devamında HttpContext.Items'tan okunur
// (Request.Cookies henüz boştur). Stitching YOK: login olunca pz_aid değişmez, satır iki kimliği
// birden taşır.
public class AnonymousIdMiddleware(RequestDelegate next)
{
    public const string AnonymousCookieName = "pz_aid";
    public const string SessionCookieName = "pz_sid";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(AnonymousCookieName, out var anonymousId)
            || !Guid.TryParse(anonymousId, out _))
        {
            anonymousId = Guid.NewGuid().ToString();
            context.Response.Cookies.Append(AnonymousCookieName, anonymousId,
                new CookieOptions { HttpOnly = true, IsEssential = true, MaxAge = TimeSpan.FromDays(365) });
        }

        if (!context.Request.Cookies.TryGetValue(SessionCookieName, out var sessionId)
            || !Guid.TryParse(sessionId, out _))
        {
            sessionId = Guid.NewGuid().ToString();
            context.Response.Cookies.Append(SessionCookieName, sessionId,
                new CookieOptions { HttpOnly = true, IsEssential = true });
        }

        context.Items[AnonymousCookieName] = anonymousId;
        context.Items[SessionCookieName] = sessionId;

        await next(context);
    }

    /// <summary>Davranış satırı için kimlik üçlüsünü çözer (anonim, oturum, varsa kullanıcı).</summary>
    public static (Guid AnonymousId, Guid SessionId, Guid? UserId) GetIds(HttpContext context)
    {
        var anonymousId = Guid.Parse((string)context.Items[AnonymousCookieName]!);
        var sessionId = Guid.Parse((string)context.Items[SessionCookieName]!);

        Guid? userId = null;
        var subject = context.User.FindFirst("sub")?.Value
                      ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (subject is not null && Guid.TryParse(subject, out var parsed)) userId = parsed;

        return (anonymousId, sessionId, userId);
    }
}