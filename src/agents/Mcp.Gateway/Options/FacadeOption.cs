namespace Mcp.Gateway.Options;

/// <summary>
/// 073/085: Fasad config (section <c>FacadeOption</c>). Hangi BC'lerin toplanacağı + keşif makine
/// kimliği. DB yok — downstream registry buradan türer. TEK uç, tek login (upfront) — anonim gez/
/// step-up modu kalıcı olarak KAPALI (kullanıcı kararı, 085): o dala ait kod yok.
/// </summary>
public class FacadeOption
{
    /// <summary>Toplanacak downstream BC'ler.</summary>
    public List<DownstreamBc> Downstreams { get; set; } = [];

    /// <summary>Keşif (ListTools) makine kimliği — client_credentials.</summary>
    public string DiscoveryClientId { get; set; } = "mcp-gateway-discovery";
    public string DiscoveryClientSecret { get; set; } = "";
    public string DiscoveryScope { get; set; } = "";

    /// <summary>Tool-katalog kısa cache (sn); 0 = cache yok (her ListTools taze).</summary>
    public int CacheTtlSeconds { get; set; } = 60;
}

/// <summary>Tek downstream BC tanımı (085: BC başına TEK entry — /mcp-admin ayrımı öldü).</summary>
public class DownstreamBc
{
    public string Name { get; set; } = "";
    public string McpUrl { get; set; } = "";
}
