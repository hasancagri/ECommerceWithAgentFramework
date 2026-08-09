using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApp.Pages.Admin;

// 032: admin metinle merchant onboarding ekrani. Yalniz admin rolu (cookie) — anonim/normal reddedilir
// (S3). Ekran BFF /chat/admin/stream'e SSE proxy'ler; o da ChatAgent 'admin' persona'ya gider.
[Authorize(Roles = "admin")]
public class OnboardingModel : PageModel
{
}