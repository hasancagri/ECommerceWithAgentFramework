# Data Model: Ürün Yorumları ve Puanlama (044)

## Reviews BC (`reviewsDb` / şema `reviewsManagement`)

### Review — Aggregate Root

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | Marten identity |
| ProductId | Guid | opak referans (Catalog ProductId; FK yok) |
| UserId | Guid | opak referans; UniqueIndex(UserId, ProductId) — R9 |
| Rating | int | 1-5 tam sayı; guard `Create`'te |
| Text | string? | opsiyonel; max 2000 karakter (kontratta sabit) |
| ReviewerName | string | ham görünen ad (token claim); yüzeye MASKELİ çıkar (R7) |
| Status | ReviewStatus | enum AYNI dosyada: `Visible=1, Hidden=2` |
| ModerationCategory | string? | Hidden ise ihlal kategorisi (agent kararından) |
| ModerationReason | string? | Hidden ise kısa gerekçe (iz; yüzeye çıkmaz) |
| ModeratedAtUtc | DateTimeOffset? | denetim tamamlanma anı (null = denetim bekliyor) |
| CreatedTime/... | — | AggregateRoot denetim alanları |

**Davranışlar** (test-first, İlke VI):

- `Create(productId, userId, rating, text, reviewerName, now)` — guard'lar: rating 1-5 tam,
  text uzunluk, ad boş olamaz. `ResultDomain<Review>`.
- `ApplyModeration(ModerationVerdict verdict, now)` — verdict.Violation ise `Status=Hidden` +
  kategori/gerekçe yazılır; değilse yalnız `ModeratedAtUtc` damgalanır. Idempotent
  (ModeratedAtUtc doluysa `Ok` no-op). `ResultDomain`.

### Value Objects (`ValueObjects/ReviewValueObjects.cs`)

- **ReviewerName** — record; `Create(raw)` (boş/whitespace red) + `Masked()`:
  her kelimenin ilk harfi + `**` ("Hasan Demiriz" → "H** D**"; tek harfli kelime olduğu gibi).
- **ModerationVerdict** — record; `Violation (bool)`, `Category (string)`, `Reason (string)`;
  `Create` guard: Violation=true iken Category boş olamaz.

### Türetilmiş özet (aggregate DEĞİL)

`ProductReviewSummary(ProductId, Average, Count)` — Visible yorumlardan Marten sorgusuyla
hesaplanır (SubmitReview/ModerateReview commit sonrası). Kalıcı tablo YOK; event payload'ı üretir.

## Shared kontratlar

- **`ReviewSummaryChanged(Guid ProductId, decimal Average, int Count)`** — integration event;
  Count=0 ⇒ tüketici özeti temizler. → contracts/review-summary-event.md
- **`order_purchase.proto`** — `OrderPurchase.HasConfirmedPurchase(HasConfirmedPurchaseRequest
  {user_id, product_id}) → HasConfirmedPurchaseReply {has_purchase}` → contracts/order-purchase-check-grpc.md

## Storefront (mevcut satıra ek)

| Alan | Tip | Not |
|---|---|---|
| RatingAverage | decimal? | null = rozet yok |
| RatingCount | int | 0 = rozet yok |

`ApplyReviewSummary(avg, count)` — Count=0 gelirse ikisini de sıfırlar/null'lar.

## Durum geçişleri

```
Create → Visible (yayın hemen; moderasyon kuyruğa)
Visible --ApplyModeration(violation)--> Hidden   (özet yeniden hesap + event)
Visible --ApplyModeration(temiz)-----> Visible  (yalnız damga)
Hidden  → (terminal; itiraz/geri alma v1 dışı)
```