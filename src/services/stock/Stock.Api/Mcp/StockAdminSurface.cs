namespace Stock.Api.Mcp;

// 070/074/085: stock admin yönetim yüzeyi politikası (Program.cs orkestrasyon dışı tutulur). 085: ayrı
// /mcp-admin ucu öldü — bu tool'lar TEK /mcp'de, token scope'una göre budanmış görünür.
public static class StockAdminSurface
{
    // Admin tool'ları (anonim çağrıda budanır — ToolScopeMap). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste.
    public static readonly string[] ToolNames =
        [Shared.StockAdminTools.SetStock, Shared.StockAdminTools.AdjustStock, Shared.StockAdminTools.ListAllStock];

    // 085 R1: tool→scope eşlemesi (tek scope — stock ayrım yapmaz). ConfigureSessionOptions bununla budar.
    public static readonly IReadOnlyDictionary<string, string> ToolScopeMap = ToolNames
        .ToDictionary(t => t, _ => AuthorizationScopes.StockWrite);
}