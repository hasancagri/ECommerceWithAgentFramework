namespace CustomNopCommerce.Domains.RewardPointsAccounts.ValueObjects;

/// <summary>
/// Ödül puanı defter kaydı — tek bir puan hareketi (kazanım pozitif, harcama negatif). nopCommerce
/// RewardPointsHistory paritesi. RewardPointsAccount aggregate'inin child'ı; bakiye bu kayıtların
/// toplamından TÜRETİLİR (ayrı alan tutulmaz). Kazanım kaydı isteğe bağlı son kullanım tarihi taşır.
/// </summary>
public record RewardPointsEntry
{
    public int Points { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public Guid? OrderId { get; private init; }
    public DateTime CreatedAtUtc { get; private init; }
    public DateTime? ExpiresAtUtc { get; private init; }

    private RewardPointsEntry() { }

    public static RewardPointsEntry Create(int points, string message, Guid? orderId,
        DateTime createdAtUtc, DateTime? expiresAtUtc) =>
        new()
        {
            Points = points,
            Message = message,
            OrderId = orderId,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
}
