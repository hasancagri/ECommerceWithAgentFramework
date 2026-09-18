namespace Customer.Api.AdminAudit;

// 078 FR-007 (070 mirası geri): admin YAZMA işlemlerinin salt-append denetim izi — aggregate DEĞİL
// (davranışsız iz dokümanı; AgentQueryLog/069 emsali). Yalnız yazma slice'ları yazar; okuma iz
// bırakmaz. Summary SIR İÇERMEZ — MerchantKey ASLA yazılmaz; ekran-link izinde token DEĞİL oturum
// Id yazılır.
public class AdminActionLog
{
    private AdminActionLog()
    {
    }

    public Guid Id { get; private set; }

    // İşlemi yapan kullanıcı (token'dan; ekran POST'unda linki üreten admin).
    public Guid UserId { get; private set; }

    // İşlem adı (tool adı ya da 078 action türü: credential_entry_link_created / merchant_credentials_submitted).
    public string Tool { get; private set; } = null!;

    // Hedef kayıt (merchant Id / oturum Id; hedefsiz işlemde "-").
    public string TargetId { get; private set; } = null!;

    // Özet değişiklik (ör. "credentials submitted, verified"); sır asla yazılmaz.
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

// Rejected = iş kuralı reddi (geçersiz ikili / PG reddi); yetki redleri endpoint'te düşer, iz bırakmaz.
public enum AdminActionVerdict
{
    Executed,
    Rejected
}