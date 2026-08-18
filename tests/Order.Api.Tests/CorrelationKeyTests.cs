using Order.Api.Domains.PaymentAttempts.ValueObjects;
using Shouldly;
using Xunit;

namespace Order.Api.Tests;

// 039 (T014, Ilke VI): correlation-key saf domain turetimi test-first. Deterministik + HMAC:
// ayni girdi -> ayni key; sepet/taksit/userId/secret degisince farkli key. Sahiplik userId'de.
public class CorrelationKeyTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Hash = "abc123";
    private const string Secret = "server-secret";

    [Fact]
    public void Same_basket_and_installment_produce_same_key()
    {
        var a = CorrelationKey.Create(User, Hash, installment: 3, Secret);
        var b = CorrelationKey.Create(User, Hash, installment: 3, Secret);

        a.Value.ShouldBe(b.Value);
    }

    [Fact]
    public void Different_basket_content_produces_different_key()
    {
        var a = CorrelationKey.Create(User, Hash, installment: 1, Secret);
        var b = CorrelationKey.Create(User, "different-hash", installment: 1, Secret);

        a.Value.ShouldNotBe(b.Value);
    }

    [Fact]
    public void Different_installment_produces_different_key()
    {
        var a = CorrelationKey.Create(User, Hash, installment: 1, Secret);
        var b = CorrelationKey.Create(User, Hash, installment: 6, Secret);

        a.Value.ShouldNotBe(b.Value);
    }

    [Fact]
    public void Different_user_produces_different_key_ownership()
    {
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var mine = CorrelationKey.Create(User, Hash, installment: 1, Secret);
        var theirs = CorrelationKey.Create(other, Hash, installment: 1, Secret);

        // Baska userId -> baska anahtar: baskasinin odemesi retrieve edilemez (FR-002c).
        mine.Value.ShouldNotBe(theirs.Value);
    }

    [Fact]
    public void Recomputes_deterministically_for_recovery()
    {
        // Kayip-yanit kurtarma: sunucu ayni girdilerden anahtari yeniden hesaplar.
        var first = CorrelationKey.Create(User, Hash, installment: 2, Secret);
        var recomputed = CorrelationKey.Create(User, Hash, installment: 2, Secret);

        recomputed.Value.ShouldBe(first.Value);
    }

    [Fact]
    public void Different_secret_produces_different_key_unforgeable()
    {
        var a = CorrelationKey.Create(User, Hash, installment: 1, Secret);
        var b = CorrelationKey.Create(User, Hash, installment: 1, "attacker-secret");

        // Secret olmadan ayni anahtar uretilemez (HMAC forge korumasi).
        a.Value.ShouldNotBe(b.Value);
    }
}
