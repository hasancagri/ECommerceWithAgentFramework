
namespace Customer.Api.Tests;

// 075 (SC-005/FR-008): kullanıcı kart izolasyonu. Handler'lar sahipliği şöyle zorlar: cüzdan HER ZAMAN
// çağıranın UserId'siyle yüklenir → o cüzdanın PgUserHandle'ı → o handle'ın PG kart listesi; foreign
// cardHandle o listede olmadığından DeleteCard/SetDefaultCard CARD_NOT_FOUND döner (handler membership
// kontrolü + live quickstart S4-negatif). Burada bu izolasyonun UNIT-testlenebilir domain çekirdeği
// doğrulanır: iki kullanıcının cüzdanları bağımsızdır ve varsayılan-kart işlemleri yalnız kendi
// cüzdanına dokunur (repo'da DB/mock altyapısı yok → handler seviyesi live doğrulanır).
public class WalletIsolationTests
{
    [Fact]
    public void TwoUsers_HaveIndependentDefaultCard()
    {
        var a = Wallet.Create(Guid.NewGuid());
        a.SetPgUserHandle("pg-user-A");
        var b = Wallet.Create(Guid.NewGuid());
        b.SetPgUserHandle("pg-user-B");

        a.SetDefaultCard("A-card-1");
        b.SetDefaultCard("B-card-1");

        // A'nın varsayılanı B'yi etkilemez, tersi de.
        a.DefaultCardHandle.ShouldBe("A-card-1");
        b.DefaultCardHandle.ShouldBe("B-card-1");

        // A varsayılanını değiştirse B değişmez.
        a.SetDefaultCard("A-card-2");
        a.DefaultCardHandle.ShouldBe("A-card-2");
        b.DefaultCardHandle.ShouldBe("B-card-1");
    }

    [Fact]
    public void ClearDefault_OnOneUser_DoesNotAffectOther()
    {
        var a = Wallet.Create(Guid.NewGuid());
        a.SetPgUserHandle("pg-user-A");
        a.SetDefaultCard("A-card-1");
        var b = Wallet.Create(Guid.NewGuid());
        b.SetPgUserHandle("pg-user-B");
        b.SetDefaultCard("B-card-1");

        // B'nin cardHandle'ıyla A üzerinde temizleme denemesi A'yı etkilemez (eşleşmez).
        a.ClearDefaultIfMatches("B-card-1");
        a.DefaultCardHandle.ShouldBe("A-card-1");

        // A kendi kartını temizler → yalnız A değişir.
        a.ClearDefaultIfMatches("A-card-1");
        a.DefaultCardHandle.ShouldBeNull();
        b.DefaultCardHandle.ShouldBe("B-card-1");
    }

    [Fact]
    public void SetDefaultCard_WithoutPgUserHandle_Rejected()
    {
        // Hiç kart eklememiş kullanıcı (PgUserHandle yok) varsayılan kart set edemez (fail-closed).
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.SetDefaultCard("foreign-card").IsSuccess.ShouldBeFalse();
        wallet.DefaultCardHandle.ShouldBeNull();
    }
}
