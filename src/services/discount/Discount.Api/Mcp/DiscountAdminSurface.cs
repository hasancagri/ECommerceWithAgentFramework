namespace Discount.Api.Mcp;

// 070/079: discount /mcp-admin yönetim yüzeyi politikası (Program.cs orkestrasyon dışı tutulur).
// Kampanya = admin işi; servisin YALNIZ /mcp-admin ucu var (anonim /mcp yok).
public static class DiscountAdminSurface
{
    // /mcp-admin ucunda GÖRÜNECEK admin tool'ları. ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste.
    public static readonly string[] ToolNames =
    [
        Shared.DiscountAdminTools.CreateCampaign,
        Shared.DiscountAdminTools.CancelCampaign,
        Shared.DiscountAdminTools.ListCampaigns,
    ];
}