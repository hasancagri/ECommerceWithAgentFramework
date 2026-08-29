using Wolverine.Persistence.Sagas;

namespace Shared;

// 049: Checkout orchestrator broker komut/yanıt sözleşmeleri (hedefli async; fanout DEĞİL).
// İlke I v1.11.0 sanctioned kanal. Düz record'lar — Wolverine [SagaIdentity] orchestrator'da uygulanır
// (Shared Wolverine'e bağımlı değil). Envelope: komut = CheckoutId + IdempotencyKey; yanıt = CheckoutId
// + Success + ErrorClass. Additive alanlar default'lu eklenir (eski tüketici kırılmaz).
public static class CheckoutMessages
{
    // Geçici (retry edilebilir) vs kalıcı (telafi/iptal gerektiren) hata ayrımı (FR-025).
    public enum ErrorClass { None = 0, Transient = 1, Permanent = 2 }

    // Ödeme kaynağı (FR-030). Charge: mock Payment BC tek-faz tahsilat (WebApp) — saga'nın SON pivot adımı.
    // AlreadyCaptured: ödeme dış PaymentGateway A2A ile ÖNCEDEN çekildi (chat/039) → orchestrator charge ATLAR
    // (çift-tahsilat yok). AlreadyCaptured'da sipariş ZATEN oluşturulmuştur (OrderId dolu gelir).
    public enum PaymentMode { Charge = 0, AlreadyCaptured = 1 }

    // Kalem: stok commit ProductId+Quantity kullanır; Order ayrıca Name+UnitPrice ister (varsayılanlı —
    // lean kullanım kırılmaz). Her checkout iki-faz mock Payment BC kullanır (tek süreç, FR-030).
    public record CheckoutItem(Guid ProductId, int Quantity, string Name = "", decimal UnitPrice = 0);

    // Sipariş adresi (Order aggregate Address VO'suna map'lenir; BC izolasyonu — düz veri taşınır).
    public record OrderAddress(string Province, string District, string Street, string ZipCode, string Line);

    // Giriş: hem WebApp Command yüzü hem chat Agent yüzü AYNI mesajı yayınlar (yalnız handler adı farklı).
    // CheckoutId = saga kimliği ([SagaIdentity] — Wolverine korelasyonu).
    public record StartCheckout(
        [property: SagaIdentity] Guid CheckoutId,
        Guid UserId,
        IReadOnlyList<CheckoutItem> Items,
        decimal Amount,
        OrderAddress Address,
        string CardRef,
        int Installments = 1,
        // 049: Charge (web, mock tek-faz ödeme) varsayılan; AlreadyCaptured (chat, dış PG çekti) OrderId dolu gelir.
        PaymentMode PaymentMode = PaymentMode.Charge,
        Guid OrderId = default);

    // --- Adım komutları (orchestrator → hedef BC) + yanıt-event'leri (→ orchestrator) ---
    // Komutlar BC handler'ında düz tüketilir (saga değil) → SagaIdentity yok. Yanıtlar saga'ya döner
    // → CheckoutId [SagaIdentity].

    public record CreateOrderCommand(Guid CheckoutId, Guid UserId, IReadOnlyList<CheckoutItem> Items, decimal Amount, OrderAddress Address, string CardRef, string IdempotencyKey);
    public record OrderCreated([property: SagaIdentity] Guid CheckoutId, Guid OrderId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record CommitStockCommand(Guid CheckoutId, Guid OrderId, Guid ProductId, Guid UserId, int Quantity, string IdempotencyKey);
    public record StockCommitted([property: SagaIdentity] Guid CheckoutId, Guid ProductId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    // Tek-faz tahsilat (pivot): stok commit sonrası SON adım. Void/refund yok — başarısızsa telafi = stok
    // revert + sipariş cancel (para hareket etmez); başarılıysa geri-alma yok.
    public record ChargePaymentCommand(Guid CheckoutId, Guid UserId, decimal Amount, int Installments, string IdempotencyKey);
    public record PaymentCharged([property: SagaIdentity] Guid CheckoutId, Guid PaymentId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record ConfirmOrderCommand(Guid CheckoutId, Guid OrderId, string IdempotencyKey);
    public record OrderConfirmed([property: SagaIdentity] Guid CheckoutId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record ClearBasketCommand(Guid CheckoutId, Guid UserId, string IdempotencyKey);
    public record BasketCleared([property: SagaIdentity] Guid CheckoutId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    // --- Telafi komutları (yalnız pivot öncesi; LIFO) ---

    public record RevertCommitStockCommand(Guid CheckoutId, Guid OrderId, Guid ProductId, Guid UserId, int Quantity, string IdempotencyKey);
    public record StockCommitReverted([property: SagaIdentity] Guid CheckoutId, Guid ProductId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record CancelOrderCommand(Guid CheckoutId, Guid OrderId, string ReasonCode, string IdempotencyKey);
    public record OrderCancelled([property: SagaIdentity] Guid CheckoutId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);
}