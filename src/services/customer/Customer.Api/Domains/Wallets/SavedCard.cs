namespace Customer.Api.Domains.Wallets;

// Wallet icinde sade entity (base almaz — BasketItem deseni). Kimligi (Id) var, bagimsiz yasamaz.
// HAM PAN/CVV TASIMAZ (tip duzeyinde yok, INV-3): tokenize sonrasi yalniz gosterilebilir alanlar.
public class SavedCard 
{
    private SavedCard()
    {
    }

    private SavedCard(Guid id, string token, string brand, string last4, int expiryMonth, int expiryYear, string? label,
        string? bin)
    {
        Id = id;
        Token = token;
        Brand = brand;
        Last4 = last4;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Label = label;
        Bin = NormalizeBin(bin);
    }

    public Guid Id { get; private set; }

    // Gateway opak token; DISA GOSTERILMEZ (yalniz revoke icin kullanilir).
    public string Token { get; private set; } = default!;
    public string Brand { get; private set; } = default!;

    public string Last4 { get; private set; } = default!;

    // 024: kartin ilk 6 hanesi (BIN). HASSAS DEGIL (karti degil bankayi tanimlar); taksit sorgusu
    // icin uzak A2A agent'a gonderilir. PAN/CVV yine SAKLANMAZ. Eski kartlarda null (BIN'siz fallback).
    public string? Bin { get; private set; }
    public int ExpiryMonth { get; private set; }
    public int ExpiryYear { get; private set; }
    public string? Label { get; private set; }
    public bool IsDefault { get; private set; }

    public static SavedCard Create(string token, string brand, string last4, int expiryMonth, int expiryYear,
        string? label, string? bin = null)
        => new(Guid.NewGuid(), token, brand, last4, expiryMonth, expiryYear, label, bin);

    // BIN = yalniz ilk 6 rakam; degilse null (bos kabul -> BIN'siz genel sorgu).
    private static string? NormalizeBin(string? bin)
    {
        if (string.IsNullOrWhiteSpace(bin)) return null;
        var digits = new string(bin.Where(char.IsDigit).ToArray());
        return digits.Length >= 6 ? digits[..6] : null;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}