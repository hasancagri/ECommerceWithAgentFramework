namespace Customer.Api.AdminAudit;

// 070 FR-009: admin YAZMA tool'larının salt-append denetim izi — aggregate DEĞİL (davranışsız iz
// dokümanı; AgentQueryLog/069 emsali). Yalnız yazma slice'ları yazar; okuma iz bırakmaz.
// Summary SIR İÇERMEZ — MerchantKey ASLA yazılmaz. BC başına kopya tip = bilinçli tekrar.
public class AdminActionLog
{
    private AdminActionLog()
    {
    }

    public Guid Id { get; private set; }

    // İşlemi yapan kullanıcı (token'dan).
    public Guid UserId { get; private set; }

    // Tool adı (Shared.McpToolNames sabiti).
    public string Tool { get; private set; } = null!;

    // Hedef kayıt (merchant Id; hedefsiz işlemde "-").
    public string TargetId { get; private set; } = null!;

    // Özet değişiklik (ör. "credentials rotated"); sır asla yazılmaz.
    public string Summary { get; private set; } = null!;

    public AdminActionVerdict Verdict { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AdminActionLog Executed(Guid userId, string tool, string targetId, string summary) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Tool = tool,
        TargetId = targetId,
        Summary = summary,
        Verdict = AdminActionVerdict.Executed,
        CreatedAt = DateTimeOffset.UtcNow
    };

    public static AdminActionLog Rejected(Guid userId, string tool, string targetId, string summary) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Tool = tool,
        TargetId = targetId,
        Summary = summary,
        Verdict = AdminActionVerdict.Rejected,
        CreatedAt = DateTimeOffset.UtcNow
    };
}

// Rejected = iş kuralı reddi (geçersiz girdi / PG reddi); yetki redleri endpoint'te düşer, iz bırakmaz.
public enum AdminActionVerdict
{
    Executed,
    Rejected
}