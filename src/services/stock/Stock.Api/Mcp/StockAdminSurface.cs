namespace Stock.Api.Mcp;

// 070/074: stock /mcp-admin yönetim yüzeyi politikası (Program.cs orkestrasyon dışı tutulur).
public static class StockAdminSurface
{
    // /mcp-admin ucunda GÖRÜNECEK admin tool'ları (anonim /mcp get_stock'ta budanır). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste.
    public static readonly string[] ToolNames =
        [Shared.StockAdminTools.SetStock, Shared.StockAdminTools.AdjustStock, Shared.StockAdminTools.ListAllStock];
}