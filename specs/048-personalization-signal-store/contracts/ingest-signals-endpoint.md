# Contract: POST /v1/signals (Gezinme Sinyali Ingest)

**Servis**: Personalization.Api. **Çağıran**: WebApp (BFF) arka plan işçisi (batch).
**Yetki**: statik scope `personalization.ingest` (client_credentials makine token'ı, D6).
**Doğa**: write-only, kayıp-toleranslı (çağıran drop edebilir).

## İstek

`POST /v1/signals`
`Authorization: Bearer <client_credentials token, scope=personalization.ingest>`
`Content-Type: application/json`

Gövde = sinyal dizisi (batch). Her öğe gezinme sinyali gövdesi (bkz
`behavior-signal-line.md`):

```json
[
  {
    "eventType": "ProductViewed",
    "channel": "web",
    "userId": "b1a2...",            // null olabilir (anonim gezinme)
    "anonymousId": "9f3c...",       // zorunlu (anonim atıf kimliği)
    "productId": "c4d5...",
    "brand": "Acme",
    "category": "Electronics",
    "price": 199.90,
    "timestamp": "2026-08-24T10:12:00Z"
  }
]
```

## Yanıt

- `202 Accepted` — batch kabul edildi (kalıcılık asenkron/senkron olabilir; çağıran
  sonucu beklemez, kayıp-toleranslı).
- `400 Bad Request` — gövde tümüyle geçersiz/parse edilemez. **Kısmi**: geçersiz tekil
  öğeler atlanır, geçerliler yazılır (FR-013); yanıt yine 2xx.
- `401/403` — scope yok/yanlış.

## Davranış

- Handler her öğeyi `BehaviorSignal.Create(...)` ile doğrular; geçersiz öğe atlanır
  (log warning), geçerli öğe `IDocumentSession.Store` + `SaveChangesAsync` (batch).
- **Kayıp-toleransı çağıranda**: WebApp `BehaviorLogWriter` kuyruğu doluysa/servis
  erişilemezse öğe düşer; endpoint down olsa bile sayfa etkilenmez (D9).
- **Liste/sonuç sayfası sinyalleri REDDEDİLİR** (049 kullanıcı kararı): ListShown /
  CategoryViewed / BrandViewed / SearchPerformed bilinen kümede değil → geçersiz öğe atlanır.
  Kalan sinyal = ProductViewed (detay) + BasketItemAdded (aksiyon).

## Versiyonlama

- URL-segment `v1`. Gövde tolerant-read ile evrilir (additive alan + default; tüketici
  bilinmeyen alanı yok sayar). `schemaVersion` alanı 049'da söküldü (sadeleştirme).

## Endpoint iskeleti (referans)

```csharp
// BehaviorSignals/BehaviorSignalEndpointExtension.cs
group.MapPost("/v1/signals", async (IngestBehaviorSignalsCommand cmd, IMessageBus bus)
        => (await bus.InvokeAsync<FeatureResultModel>(cmd)) is { IsSuccess: true }
            ? Results.Accepted() : Results.BadRequest())
     .RequireAuthorization(/* personalization.ingest scope policy */);
```