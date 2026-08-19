using CustomNopCommerce.Domains.RewardPointsAccounts.ValueObjects;

namespace CustomNopCommerce.Domains.RewardPointsAccounts;

/// <summary>
/// Ödül puanı hesabı — Loyalty bounded context'inin aggregate kökü, müşteri başına BİR tane. nopCommerce
/// puanları düz history satırlarıyla tutar; burada defter (ledger) tek aggregate'e toplandı. Zengin aggregate
/// dersleri: (1) bakiye TÜRETİLİR (<see cref="Balance"/> = Σ giriş puanı); (2) harcama invariant'ı
/// (bakiyeden fazlası harcanamaz); (3) hesap = defter, her hareket bir <see cref="RewardPointsEntry"/> child.
/// CustomerId opak referanstır (Customer BC). Bu, nopCommerce'in Customer god-entity'sinden çıkarılan RewardPoints'in yeri.
/// </summary>
public class RewardPointsAccount : AggregateRoot
{
    public Guid CustomerId { get; private set; }

    private readonly List<RewardPointsEntry> _entries = new();
    public IReadOnlyList<RewardPointsEntry> Entries => _entries;

    // Türetilmiş bakiye = tüm hareketlerin toplamı (kazanım + / harcama −). Ayrı alan tutulmaz.
    public int Balance => _entries.Sum(e => e.Points);

    private RewardPointsAccount() { }

    /// <summary>Bir müşteri için yeni puan hesabı açar (sıfır bakiye).</summary>
    /// <remarks>Handler: EarnPointsCommandHandler</remarks>
    public static RewardPointsAccount Create(Guid customerId) =>
        new() { CustomerId = customerId };

    /// <summary>Puan kazandırır (pozitif hareket). Puan > 0 olmalı.</summary>
    /// <remarks>Handler: EarnPointsCommandHandler</remarks>
    public ResultDomain Earn(int points, string message, Guid? orderId, DateTime nowUtc, DateTime? expiresAtUtc)
    {
        if (points <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(points), Code = LoyaltyResourceConstants.POINTS_INVALID });
        _entries.Add(RewardPointsEntry.Create(points, message, orderId, nowUtc, expiresAtUtc));
        return ResultDomain.Ok();
    }

    /// <summary>Puan harcar (negatif hareket). Puan > 0 ve bakiyeyi aşmamalı (invariant).</summary>
    /// <remarks>Handler: RedeemPointsCommandHandler</remarks>
    public ResultDomain Redeem(int points, string message, Guid? orderId, DateTime nowUtc)
    {
        if (points <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(points), Code = LoyaltyResourceConstants.POINTS_INVALID });
        if (points > Balance)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(points), Code = LoyaltyResourceConstants.INSUFFICIENT_POINTS });
        _entries.Add(RewardPointsEntry.Create(-points, message, orderId, nowUtc, null));
        return ResultDomain.Ok();
    }
}
