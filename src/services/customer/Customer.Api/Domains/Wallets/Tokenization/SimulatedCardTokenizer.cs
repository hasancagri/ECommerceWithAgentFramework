namespace Customer.Api.Domains.Wallets.Tokenization;

// Stub tokenizer (gateway gelene kadar). State'siz -> Singleton. Gateway HENUZ YOK; her cagriyi
// BASARILI (gateway sonuc donmuS gibi) doner. Ileride gercek gateway cagrisiyla swap edilir —
// Wallet kodu DEGISMEZ. Kart dogrulamasi (gecmis expiry/bos PAN) domain aggregate'inde yapilir;
// stub yalniz gateway'in cevap seklini (token + brand + last4) taklit eder. PAN/CVV'yi hicbir
// yere yazmaz/loglamaz.
public class SimulatedCardTokenizer : ICardTokenizer, ISingletonDependency
{
    public Task<TokenizeResult> TokenizeAsync(
        string pan, string cvv, int expiryMonth, int expiryYear, CancellationToken ct)
    {
        var digits = new string((pan ?? string.Empty).Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;

        // Sahte marka — yalniz demo icin placeholder. Gercek marka gercek gateway'den gelecek.
        var brand = digits.Length > 0 && digits[0] == '4' ? "Visa"
            : digits.Length > 0 && digits[0] == '5' ? "Mastercard"
            : "Card";

        // Opak token — PAN'i icermez, geri cozulemez (stub: rastgele tutamac).
        var token = $"tok_{Guid.NewGuid():N}";

        return Task.FromResult(new TokenizeResult(true, token, brand, last4, null));
    }

    public Task RevokeAsync(string token, CancellationToken ct) => Task.CompletedTask;
}