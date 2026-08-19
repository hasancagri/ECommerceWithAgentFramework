using System.Security.Cryptography;
using System.Text;

namespace Order.Api.Domains.PaymentAttempts.ValueObjects;

// 039: cekim idempotency + sahiplik anahtari. Deterministik HMAC: HMAC(serverSecret, userId|hash|installment).
// - Ayni sepet+taksit -> ayni anahtar -> dogal idempotency (retry cift cekmez).
// - Sepet degisince (contentHash) veya taksit degisince anahtar degisir -> yeni cekim dogru.
// - userId icermesi + HMAC olmasi anahtari SAHIPLIK-tasiyici + FORGE-edilemez yapar: baska kullanici
//   (baska userId) veya secret'siz taraf ayni anahtari uretemez -> baskasinin odemesi retrieve edilemez.
// - Sunucu her an yeniden hesaplayabilir (kayip-yanit kurtarmanin dayanagi). Secret disaridan verilir
//   (VO config bilmez, saf kalir — CorrelationKeyOption'i handler gecirir).
public sealed record CorrelationKey
{
    public string Value { get; }

    private CorrelationKey(string value) => Value = value;

    public static CorrelationKey Create(Guid userId, string basketContentHash, int installment, string serverSecret)
    {
        var payload = $"{userId:N}|{basketContentHash}|{installment}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(serverSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return new CorrelationKey(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public override string ToString() => Value;
}
