namespace Customer.Api.Tests;

public class MerchantInformationTests
{
    private static readonly Guid ValidMerchant = Guid.NewGuid();
    private const string ValidKey = "mk_abc123";

    [Fact]
    public void Create_gecerli_Ok_ve_Active()
    {
        var result = MerchantInformation.Create(ValidMerchant, ValidKey);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.MerchantId.ShouldBe(ValidMerchant);
        result.Data.MerchantKey.ShouldBe(ValidKey);
        result.Data.Status.ShouldBe("Active");
    }

    [Fact]
    public void Create_bosluklu_key_trimlenir()
    {
        var result = MerchantInformation.Create(ValidMerchant, "  mk_x  ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.MerchantKey.ShouldBe("mk_x");
    }

    [Fact]
    public void Create_bos_merchantId_Error()
    {
        var result = MerchantInformation.Create(Guid.Empty, ValidKey);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_bos_key_Error()
    {
        var result = MerchantInformation.Create(ValidMerchant, "   ");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void UpdateKey_gecerli_key_gunceller()
    {
        var info = MerchantInformation.Create(ValidMerchant, ValidKey).Data!;

        var result = info.UpdateKey("mk_new");

        result.IsSuccess.ShouldBeTrue();
        info.MerchantKey.ShouldBe("mk_new");
        info.MerchantId.ShouldBe(ValidMerchant); // kimlik değişmez
    }

    [Fact]
    public void UpdateKey_bos_Error_ve_eski_key_korunur()
    {
        var info = MerchantInformation.Create(ValidMerchant, ValidKey).Data!;

        var result = info.UpdateKey("  ");

        result.IsSuccess.ShouldBeFalse();
        info.MerchantKey.ShouldBe(ValidKey);
    }
}
