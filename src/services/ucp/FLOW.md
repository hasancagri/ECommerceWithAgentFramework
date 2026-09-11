# UCP Checkout Kanalı — Domain Süreci (FLOW)

Dış AI platformları için UCP-uyumlu checkout kanalı: keşif → session (create/update/kargo/indirim) →
already-captured sipariş devri → imzalı sipariş-olayı webhook'u. İç web/saga checkout'a dokunmaz.

## Süreç

1. Platform keşif profilini çeker — capabilities/uzantılar/ödeme handler/public anahtarlar.
2. Platform checkout session açar — kalemler + para birimi + yasal linkler (`UcpCheckoutSession.Create`).
3. Session güncellenir — kalem tam değişim + alıcı + kargo seçimi + indirim kodu; toplamlar yeniden
   hesaplanır (`ReplaceLineItems` / `SelectFulfillment` / `ApplyDiscounts` → totals).
4. Hazır olunca `ready_for_complete` (`MarkReadyIfComplete`).
5. Complete — süre/hazırlık guard + idempotency (`BeginComplete`); toplam tazelenir; Order'a gRPC
   `CreateExternalOrder` (charge PG/iyzico sandbox + AlreadyCaptured); başarı → `MarkCompleted(orderRef)`.
6. Sipariş durum değişimi (onay/iptal) → platforma imzalı webhook (RFC 9421), retry.

## Domain kuralları

- Para birimi TRY; `links` zorunlu; oluşturmada `expires_at` = +6h.
- Süresi dolmuş session complete edilemez.
- `line_items` boş session `ready` olamaz.
- Complete idempotent — aynı `IdempotencyKey`/session tek `OrderRef`.
- Totals her değişimde + complete anında yeniden hesaplanır (stale toplamla tahsilat yok).
- `requires_escalation` yalnız `ContinueUrl` varken.

## Sınır (dokunmadığı)

- İç web/saga checkout akışı (paralel kanal; yalnız already-captured devir noktası).
- PaymentGateway (charge Order içinde; UCP BC PG'ye doğrudan dokunmaz).
- Catalog/Stock aggregate'leri (kendi projeksiyonunu olay tüketerek kurar — İlke I).