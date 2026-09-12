
namespace Customer.Api.Tests;

// 075: ince Wallet — yerel kart deposu yok; yalnız PG kullanıcı-handle çapası + varsayılan kart tercihi.
// İlke VI: davranış + invariant test-first.
public class WalletTests
{
    [Fact]
    public void Create_SetsUserId_AndNoHandles()
    {
        var userId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);

        wallet.UserId.ShouldBe(userId);
        wallet.PgUserHandle.ShouldBeNull();
        wallet.DefaultCardHandle.ShouldBeNull();
    }

    [Fact]
    public void SetPgUserHandle_Rejects_Empty()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.SetPgUserHandle("").IsSuccess.ShouldBeFalse();
        wallet.SetPgUserHandle("   ").IsSuccess.ShouldBeFalse();
        wallet.PgUserHandle.ShouldBeNull();
    }

    [Fact]
    public void SetPgUserHandle_SetsFirst_ThenIdempotent()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.SetPgUserHandle("pg-user-1").IsSuccess.ShouldBeTrue();
        wallet.PgUserHandle.ShouldBe("pg-user-1");

        // Aynı kullanıcı = aynı handle; ikinci ekleme yeni handle YAZMAZ (idempotent).
        wallet.SetPgUserHandle("pg-user-2").IsSuccess.ShouldBeTrue();
        wallet.PgUserHandle.ShouldBe("pg-user-1");
    }

    [Fact]
    public void SetDefaultCard_Requires_PgUserHandle()
    {
        var wallet = Wallet.Create(Guid.NewGuid());

        wallet.SetDefaultCard("card-1").IsSuccess.ShouldBeFalse();
        wallet.DefaultCardHandle.ShouldBeNull();
    }

    [Fact]
    public void SetDefaultCard_Overwrites_KeepingSingleDefault()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.SetPgUserHandle("pg-user-1");

        wallet.SetDefaultCard("card-1").IsSuccess.ShouldBeTrue();
        wallet.DefaultCardHandle.ShouldBe("card-1");

        wallet.SetDefaultCard("card-2").IsSuccess.ShouldBeTrue();
        wallet.DefaultCardHandle.ShouldBe("card-2");
    }

    [Fact]
    public void ClearDefaultIfMatches_ClearsOnlyOnMatch()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.SetPgUserHandle("pg-user-1");
        wallet.SetDefaultCard("card-1");

        // Eşleşmeyen silme → varsayılan durur.
        wallet.ClearDefaultIfMatches("card-2").IsSuccess.ShouldBeTrue();
        wallet.DefaultCardHandle.ShouldBe("card-1");

        // Eşleşen silme → varsayılan null.
        wallet.ClearDefaultIfMatches("card-1").IsSuccess.ShouldBeTrue();
        wallet.DefaultCardHandle.ShouldBeNull();
    }

    [Fact]
    public void MarkFirstCardDefault_SetsWhenEmpty_NoOpWhenSet()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.SetPgUserHandle("pg-user-1");

        // Varsayılan boşsa ilk kart varsayılan olur (FR-001a).
        wallet.MarkFirstCardDefault("card-1").IsSuccess.ShouldBeTrue();
        wallet.DefaultCardHandle.ShouldBe("card-1");

        // Zaten varsayılan varsa dokunmaz (no-op).
        wallet.MarkFirstCardDefault("card-2").IsSuccess.ShouldBeTrue();
        wallet.DefaultCardHandle.ShouldBe("card-1");
    }
}
