# Tokenizer Contract — ICardTokenizer

Wallet'ın ham PAN/CVV'ye dokunmadan token almasını sağlayan soyut sınır. Bu iterasyonda
`SimulatedCardTokenizer` (stub) arkasındadır; PaymentGateway (ayrı repo) gelince yalnız stub
gerçek çağrıyla değişir — **Wallet kodu değişmez** (spec Assumptions, memory card-vault).

## Arayüz (öneri)

```csharp
public interface ICardTokenizer   // ISingletonDependency (state'siz)
{
    Task<TokenizeResult> TokenizeAsync(
        string pan, string cvv, int expiryMonth, int expiryYear, CancellationToken ct);

    // Kart silme/güncelleme sonrası orphan token'ı gateway vault'ta geçersizler.
    // Stub: no-op. Idempotent (bilinmeyen token → sorunsuz).
    Task RevokeAsync(string token, CancellationToken ct);
}

public sealed record TokenizeResult(
    bool Success, string? Token, string? Brand, string? Last4, string? ErrorCode);
```

## Sözleşme kuralları

- **Girdi**: ham PAN + CVV + son-kullanma. Yalnız bu çağrıda görülür; çağıran (AddCard handler)
  bunları hiçbir yere yazmaz/loglamaz.
- **Çıktı (başarı)**: `Success=true`, opak `Token`, `Brand`, `Last4`. PAN/CVV **dönmez**.
- **Çıktı (hata)**: `Success=false`, `ErrorCode` (resource sabitine eşlenir); Token null.
  Çağıran fail-closed: hiçbir şey `Store` etmez (FR-013).
- **Stub davranışı**: geçerli-görünen PAN için sahte deterministik-olmayan token üretir; Brand'ı
  ilk haneden (4→Visa, 5→Mastercard...) çıkarır; Last4 = PAN son 4. Geçmiş son-kullanma veya
  boş PAN → `Success=false`.
- **İdempotency**: gerekmez (mükerrer kart sessiz kabul; spec Assumptions).

## Güvenlik notları

- Bu kontrat **asla** MCP tool'u olarak açılmaz (ham PAN LLM turuna girmez, FR-019).
- Token opak tutamaçtır; kullanıcı ayrımı token'da değil Wallet `UserId`'sindedir.