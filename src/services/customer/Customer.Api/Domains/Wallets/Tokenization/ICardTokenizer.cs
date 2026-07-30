namespace Customer.Api.Domains.Wallets.Tokenization;

// Wallet'in ham PAN/CVV'ye dokunmadan token almasini saglayan soyut sinir. Bu iterasyonda
// SimulatedCardTokenizer (stub) arkasindadir; PaymentGateway (ayri repo) gelince yalniz stub
// gercek cagriyla degisir — Wallet kodu degismez.
public interface ICardTokenizer
{
    // Ham PAN + CVV yalniz bu cagrida gorulur; cagiran (AddCard handler) hicbir yere yazmaz/loglamaz.
    Task<TokenizeResult> TokenizeAsync(
        string pan, string cvv, int expiryMonth, int expiryYear, CancellationToken ct);

    // Kart silme/guncelleme sonrasi orphan token'i gateway vault'ta gecersizler. Stub: no-op.
    // Idempotent (bilinmeyen token -> sorunsuz). Fail-open: cagiran hatayi yutar, silme bloklanmaz.
    Task RevokeAsync(string token, CancellationToken ct);
}

// Basari: Success=true + opak Token + Brand + Last4 (PAN/CVV DONMEZ).
// Hata: Success=false + ErrorCode (resource sabitine eslenir); Token null.
public sealed record TokenizeResult(
    bool Success, string? Token, string? Brand, string? Last4, string? ErrorCode);