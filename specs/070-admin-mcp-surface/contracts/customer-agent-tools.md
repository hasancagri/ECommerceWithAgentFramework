# Contract: Müşteri Agent Tool'u — quote_installments (070 / US4)

Uç: Order.Api MEVCUT korumalı `/mcp` (basket/order deseni; yeni uç yok). Ad: `OrderTools.QuoteInstallments`.

## `quote_installments` — scope: `order.read` + `payment.read`

| Alan | Değer |
|---|---|
| Parametreler | cardId (Guid?, opsiyonel — MCP default kuralı; yoksa varsayılan kart) |
| Dönüş | {basketTotal, options: [{installmentNumber (int), totalPrice (decimal)}], message?} |
| Boş sepet | iş hatası: "önce sepete ürün ekleyin" (Result kodu; teknik detay yok) |
| Kart/adres yok | yönlendirici iş hatası (mevcut chat kural 8 davranışının sunucu karşılığı) |
| PG ulaşılamaz | "şu an yapılamıyor" iş hatası; exception sızmaz |

## İç zincir (PlaceOrderForAgent şablonu — yalnız quote'a kadar)

1. Basket gRPC `GetItemsAsync` → sepet toplamı (ContentHash kullanılmaz; çekim yok).
2. Customer S2S `api/v1/internal/payment-context` (makine token) → vaultToken + merchantId + buyer.
3. PG A2A `quote-installments` skill'i (Order.Api içinde A2A istemcisi; PG'ye DOKUNULMAZ).
4. Yanıt süzülür: yalnız installmentNumber + totalPrice döner; vaultToken/buyer/merchantKey ASLA.

Not: tek okuma işlemi — AdminActionLog yazılmaz (FR-009 yalnız yazma). ChatAgent'ın A2A tool'u bu
feature'da SİLİNMEZ (söküm feature'ının işi); iki yol geçici paralel yaşar.
