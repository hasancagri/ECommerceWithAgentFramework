namespace Customer.Api.Domains.Wallets;

// 075: hosted kart-ekleme callback'ini kullanıcıya bağlayan TEK-KULLANIMLIK korelasyon kaydı (R3).
// Aggregate DEĞİL — süreç korelasyonu (Marten doc). PG'ye conversationId olarak Id gider; kullanıcı
// formu bitirince dönüş (JWT'siz callback) bu Id ile UserId'yi çözer. Süre-sınırlı + tek-kullanımlık:
// Completed/Cancelled/Expired olan tekrar kullanılamaz.
public class AddCardSession
{
    private AddCardSession() { }

    // Tahmin-edilemez session kimliği; PG'ye conversationId olarak verilir.
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public AddCardSessionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Yeni bir Pending oturum başlatır (verilen kullanıcı için).</summary>
    public static AddCardSession Start(Guid userId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Status = AddCardSessionStatus.Pending,
        CreatedAt = now
    };

    /// <summary>Oturum callback için hâlâ kullanılabilir mi (Pending + süre dolmamış).</summary>
    public bool IsUsable(DateTimeOffset now, TimeSpan ttl) =>
        Status == AddCardSessionStatus.Pending && now - CreatedAt <= ttl;

    /// <summary>Süre dolmuş oturumu Expired işaretler (idempotent yardımcı).</summary>
    public void Expire() => Status = AddCardSessionStatus.Expired;

    /// <summary>Kart başarıyla eklendi — oturum tüketildi.</summary>
    public void Complete() => Status = AddCardSessionStatus.Completed;

    /// <summary>Kullanıcı iptal etti / PG hata → oturum tüketildi, kayıt yapılmaz (FR-010).</summary>
    public void Cancel() => Status = AddCardSessionStatus.Cancelled;
}

public enum AddCardSessionStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
    Expired = 3
}
