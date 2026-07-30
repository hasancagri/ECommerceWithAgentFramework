namespace Customer.Api.Domains.Wallets;

// Wallet icinde sade entity (base almaz — BasketItem deseni). Kimligi (Id) var, bagimsiz yasamaz.
// HAM PAN/CVV TASIMAZ (tip duzeyinde yok, INV-3): tokenize sonrasi yalniz gosterilebilir alanlar.
public class SavedCard
{
    private SavedCard() { }

    private SavedCard(Guid id, string token, string brand, string last4, int expiryMonth, int expiryYear, string? label)
    {
        Id = id;
        Token = token;
        Brand = brand;
        Last4 = last4;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Label = label;
    }

    public Guid Id { get; private set; }
    // Gateway opak token; DISA GOSTERILMEZ (yalniz revoke icin kullanilir).
    public string Token { get; private set; } = default!;
    public string Brand { get; private set; } = default!;
    public string Last4 { get; private set; } = default!;
    public int ExpiryMonth { get; private set; }
    public int ExpiryYear { get; private set; }
    public string? Label { get; private set; }
    public bool IsDefault { get; private set; }

    public static SavedCard Create(string token, string brand, string last4, int expiryMonth, int expiryYear, string? label)
        => new(Guid.NewGuid(), token, brand, last4, expiryMonth, expiryYear, label);

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}