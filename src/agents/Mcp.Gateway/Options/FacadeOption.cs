namespace Mcp.Gateway.Options;

/// <summary>
/// 073: Fasad config (section <c>FacadeOption</c>). Hangi BC'lerin, hangi yüzeyde toplanacağı + keşif
/// makine kimliği + auth zamanlaması. DB yok — downstream registry buradan türer.
/// </summary>
public class FacadeOption
{
    /// <summary>Toplanacak downstream BC'ler (yüzey + korumalı-mı bilgisiyle).</summary>
    public List<DownstreamBc> Downstreams { get; set; } = [];

    /// <summary>Keşif (ListTools) makine kimliği — client_credentials.</summary>
    public string DiscoveryClientId { get; set; } = "mcp-gateway-discovery";
    public string DiscoveryClientSecret { get; set; } = "";
    public string DiscoveryScope { get; set; } = "";

    /// <summary>Tool-katalog kısa cache (sn); 0 = cache yok (her ListTools taze).</summary>
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Auth zamanlaması. <c>true</c> = bağlanınca tek login (garantili taban; anonim yok). <c>false</c> =
    /// anonim gez+sepet + checkout'ta step-up login (denenir; canlı tutmazsa true'ya çekilir — kullanıcı kararı).
    /// </summary>
    public bool RequireLoginUpfront { get; set; } = true;
}

/// <summary>Tek downstream BC tanımı.</summary>
public class DownstreamBc
{
    public string Name { get; set; } = "";
    public string McpUrl { get; set; } = "";
    /// <summary><c>customer</c> | <c>admin</c> — hangi fasad ucunda görünür.</summary>
    public string Surface { get; set; } = "customer";
    /// <summary>Tool'ları korumalı mı (checkout/hesap → step-up) yoksa anonim mi (katalog/vitrin).</summary>
    public bool RequiresUserAuth { get; set; }
}