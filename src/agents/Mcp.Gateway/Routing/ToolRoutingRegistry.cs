namespace Mcp.Gateway.Routing;

/// <summary>
/// 073: tool-adı → sahip BC eşlemesi (saf). ListTools toplaması sonrası kurulur; CallTool bununla
/// yönlenir. Tool adları global benzersiz (Shared/McpToolNames) → namespace yok. Aynı ad iki BC'den
/// gelirse deterministik İLK-sahip kazanır (Add ikinciyi yok sayar, false döner — çağıran loglar).
/// </summary>
public sealed class ToolRoutingRegistry
{
    private readonly Dictionary<string, DownstreamBc> _owners = new(StringComparer.Ordinal);

    /// <summary>Adı sahibine ekler; ad zaten varsa yok sayar ve <c>false</c> döner (ilk-sahip kuralı).</summary>
    public bool Add(string toolName, DownstreamBc owner)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return false;
        return _owners.TryAdd(toolName, owner);
    }

    /// <summary>Adın sahibini çözer; kayıtta yoksa null.</summary>
    public DownstreamBc? Resolve(string toolName)
        => toolName is not null && _owners.TryGetValue(toolName, out var bc) ? bc : null;

    public IReadOnlyCollection<string> ToolNames => _owners.Keys;

    public int Count => _owners.Count;
}