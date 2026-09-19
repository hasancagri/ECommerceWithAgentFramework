namespace Customer.Api.Tests;

// 078 İLKE VI: saf domain test-first — tek kullanımlık credential-giriş ekran oturumu
// (Create/Consume/IsUsable) + MerchantInformation CredentialsVerified davranışı.
public class CredentialEntrySessionTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    [Fact]
    public void Create_gecerli_Ok_token_uretir_sure_kurar()
    {
        var before = DateTimeOffset.UtcNow;
        var result = CredentialEntrySession.Create(Admin, Lifetime);

        result.IsSuccess.ShouldBeTrue();
        var session = result.Data!;
        session.Token.ShouldNotBeNullOrWhiteSpace();
        session.RequestedByUserId.ShouldBe(Admin);
        session.ConsumedAt.ShouldBeNull();
        session.ExpiresAt.ShouldBeGreaterThanOrEqualTo(before + Lifetime);
        session.ExpiresAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow + Lifetime);
    }

    [Fact]
    public void Create_token_url_safe_ve_yeterince_uzun()
    {
        var token = CredentialEntrySession.Create(Admin, Lifetime).Data!.Token;

        // 256-bit = 32 bayt → base64url 43 karakter; +, / ve = URL'e giremez.
        token.Length.ShouldBeGreaterThanOrEqualTo(43);
        token.ShouldNotContain("+");
        token.ShouldNotContain("/");
        token.ShouldNotContain("=");
    }

    [Fact]
    public void Create_iki_oturum_farkli_token()
    {
        var first = CredentialEntrySession.Create(Admin, Lifetime).Data!.Token;
        var second = CredentialEntrySession.Create(Admin, Lifetime).Data!.Token;

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Create_bos_kullanici_Error()
    {
        var result = CredentialEntrySession.Create(Guid.Empty, Lifetime);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_pozitif_olmayan_sure_Error()
    {
        var result = CredentialEntrySession.Create(Admin, TimeSpan.Zero);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Consume_mutlu_yol_Ok_ve_oturum_olur()
    {
        var session = CredentialEntrySession.Create(Admin, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;

        var result = session.Consume(now);

        result.IsSuccess.ShouldBeTrue();
        session.ConsumedAt.ShouldBe(now);
        session.IsUsable(now).ShouldBeFalse();
    }

    [Fact]
    public void Consume_ikinci_kez_Error()
    {
        var session = CredentialEntrySession.Create(Admin, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;
        session.Consume(now).IsSuccess.ShouldBeTrue();

        var second = session.Consume(now.AddSeconds(1));

        second.IsSuccess.ShouldBeFalse();
        session.ConsumedAt.ShouldBe(now); // ilk tüketim anı korunur
    }

    [Fact]
    public void Consume_suresi_gecmis_Error()
    {
        var session = CredentialEntrySession.Create(Admin, Lifetime).Data!;
        var afterExpiry = session.ExpiresAt.AddSeconds(1);

        var result = session.Consume(afterExpiry);

        result.IsSuccess.ShouldBeFalse();
        session.ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public void IsUsable_taze_true_suresi_gecmis_false()
    {
        var session = CredentialEntrySession.Create(Admin, Lifetime).Data!;

        session.IsUsable(DateTimeOffset.UtcNow).ShouldBeTrue();
        session.IsUsable(session.ExpiresAt.AddSeconds(1)).ShouldBeFalse();
    }
}

// 078 T009: MerchantInformation credential-set davranışı CredentialsVerified bayrağını taşır.
public class MerchantInformationCredentialsVerifiedTests
{
    private static readonly Guid MerchantId = Guid.NewGuid();

    [Fact]
    public void Create_verified_bayragi_yazilir()
    {
        var verified = MerchantInformation.Create(MerchantId, "mk_x", credentialsVerified: true).Data!;
        var unverified = MerchantInformation.Create(MerchantId, "mk_x", credentialsVerified: false).Data!;

        verified.CredentialsVerified.ShouldBeTrue();
        unverified.CredentialsVerified.ShouldBeFalse();
    }

    [Fact]
    public void UpdateKey_verified_bayragini_gunceller()
    {
        var info = MerchantInformation.Create(MerchantId, "mk_x", credentialsVerified: true).Data!;

        var result = info.UpdateKey("mk_new", credentialsVerified: false);

        result.IsSuccess.ShouldBeTrue();
        info.MerchantKey.ShouldBe("mk_new");
        info.CredentialsVerified.ShouldBeFalse();
    }

    [Fact]
    public void UpdateKey_bos_key_Error_bayrak_degismez()
    {
        var info = MerchantInformation.Create(MerchantId, "mk_x", credentialsVerified: true).Data!;

        var result = info.UpdateKey("  ", credentialsVerified: false);

        result.IsSuccess.ShouldBeFalse();
        info.CredentialsVerified.ShouldBeTrue();
    }
}