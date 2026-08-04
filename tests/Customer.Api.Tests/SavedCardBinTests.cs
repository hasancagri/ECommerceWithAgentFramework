namespace Customer.Api.Tests;

// 024: SavedCard BIN yakalama + normalizasyon. BIN = ilk 6 hane (hassas degil); gecersiz/eksikse
// null (BIN'siz genel taksit sorgusu fallback). PAN/CVV yine saklanmaz (INV-3, WalletTests'te).
public class SavedCardBinTests
{
    [Fact]
    public void Create_CapturesBin_First6Digits()
    {
        var card = SavedCard.Create("tok", "Visa", "1111", 12, 2030, null, "552879");

        card.Bin.ShouldBe("552879");
    }

    [Fact]
    public void Create_NormalizesBin_ToFirst6_WhenLonger()
    {
        // Tam PAN verilse bile yalniz ilk 6 tutulur (savunma).
        var card = SavedCard.Create("tok", "Visa", "1111", 12, 2030, null, "5528790000001111");

        card.Bin.ShouldBe("552879");
    }

    [Fact]
    public void Create_StripsNonDigits_BeforeTakingBin()
    {
        var card = SavedCard.Create("tok", "Visa", "1111", 12, 2030, null, "5528 79xx");

        card.Bin.ShouldBe("552879");
    }

    [Fact]
    public void Create_NullBin_WhenMissing()
    {
        var card = SavedCard.Create("tok", "Visa", "1111", 12, 2030, null);

        card.Bin.ShouldBeNull();
    }

    [Fact]
    public void Create_NullBin_WhenFewerThan6Digits()
    {
        var card = SavedCard.Create("tok", "Visa", "1111", 12, 2030, null, "5528");

        card.Bin.ShouldBeNull();
    }
}