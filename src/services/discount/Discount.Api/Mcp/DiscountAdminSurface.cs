namespace Discount.Api.Mcp;

// 070/079/085 R5: discount yönetim yüzeyi politikası (Program.cs orkestrasyon dışı tutulur).
// Kampanya = admin işi; servisin YALNIZ korumalı /mcp ucu var (anonim set yok; /mcp-admin öldü).
public static class DiscountAdminSurface
{
    // Admin tool'ları (scope yoksa boş liste — ToolScopeMap). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste.
    public static readonly string[] ToolNames =
    [
        Shared.DiscountAdminTools.CreateCampaign,
        Shared.DiscountAdminTools.CancelCampaign,
        Shared.DiscountAdminTools.ListCampaigns,
    ];

    // 085 R1: tool→scope eşlemesi (tek scope — discount ayrım yapmaz). ConfigureSessionOptions bununla budar.
    public static readonly IReadOnlyDictionary<string, string> ToolScopeMap = ToolNames
        .ToDictionary(t => t, _ => AuthorizationScopes.AdminDiscountWrite);
}