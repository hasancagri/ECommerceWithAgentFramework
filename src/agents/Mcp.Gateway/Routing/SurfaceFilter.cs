namespace Mcp.Gateway.Routing;

/// <summary>
/// 073: yüzey ayrımı (saf). <c>/mcp</c> → customer downstream'leri; <c>/mcp-admin</c> → admin. Çapraz
/// sızıntı yasak (SC-006). Yüzey string'i büyük/küçük harf duyarsız eşleşir.
/// </summary>
public static class SurfaceFilter
{
    public const string Customer = "customer";
    public const string Admin = "admin";

    public static IEnumerable<DownstreamBc> ForSurface(IEnumerable<DownstreamBc> downstreams, string surface)
        => downstreams.Where(d => string.Equals(d.Surface, surface, StringComparison.OrdinalIgnoreCase));

    /// <summary>İstek yolundan yüzey: <c>/mcp-admin*</c> → admin, aksi → customer.</summary>
    public static string FromPath(string path)
        => path.StartsWith("/mcp-admin", StringComparison.OrdinalIgnoreCase) ? Admin : Customer;
}