using Hangfire.Dashboard;

namespace Supplier.Gateway;

// Pano yalnız Development'ta map'lenir (FR-008); erişim niyeti örtük local-only filtreye
// bırakılmaz — Aspire proxy'si istekleri localhost'tan geçirdiği için o filtre yanıltıcı olur.
public sealed class HangfireDashboardAllowAllFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}