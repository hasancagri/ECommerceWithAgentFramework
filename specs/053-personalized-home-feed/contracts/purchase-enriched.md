# Contract — PurchaseEnriched (Storefront → Python, broker event)

**Yön:** Storefront → `reco_trainer` (Python). **Kanal:** RabbitMQ fanout (integration event; durable,
at-least-once). **Sahiplik:** Storefront yayıncı (enricher), Python tüketici (idempotent).

## Neden bu event

`OrderCompleted` (mevcut) item'ları **yazar/kategori taşımaz** (Order BC izolasyonu). Satın-alma = en güçlü niyet
sinyali; öznitelik-bazlı profile katkı için yazar/kategori gerekir. Storefront `StorefrontView`'de bu öznitelikler
denormalize durur → Storefront `OrderCompleted`'ı tüketip **zenginleştirir**, `PurchaseEnriched` yayar. Python
katalog bilmez, tek `Signal` tablosuna yazar.

## Akış

```
Order ──OrderCompleted──> Storefront (durable inbox exactly-once; her item: StorefrontView'den +author +category +dedupKey) ──PurchaseEnriched──> Python (unique dedup_key)
```

## Şema (Shared.IntegrationEvents)

```json
{
  "orderId": "e1…",
  "userId": "9c…",
  "anonymousId": "1b9f…",              // varsa (dikiş); yoksa null
  "occurredAt": "2026-08-31T10:20:00Z",
  "items": [
    { "productId": "a3…", "quantity": 2, "unitPrice": 45.0,
      "author": "Tolstoy", "category": "Tarih",     // Storefront doldurdu; katalogda yoksa null
      "dedupKey": "f2c9…" }                          // Storefront Guid.NewGuid() (bir kez, kalıcı) — son-hat idempotency
  ]
}
```

**Kurallar:**
- **Üst hat:** Storefront `OrderCompleted`'ı **ayrı kuyrukta `.UseDurableInbox()`** ile exactly-once işler → item `dedupKey`'i bir kez üretilir, outbox'ta kalıcı (tekrar teslimde aynı gider).
- **Idempotent tüketim (son hat):** Python `unique(dedup_key)` → `PurchaseEnriched` yeniden teslim edilirse no-op (çift sayma yok).
- Her item → `event_type="Purchased"` satırı (tek `Signal` tablosu). Puan `eventType`'tan config'le türetilir (en yüksek).
- `author`/`category` katalogda bulunamazsa null; o kalem o boyutta profile katkı vermez, akışı bozmaz.
- **Binding'i TÜKETİCİ kurar** (Python) — soğuk-açılış kayıp dersi. Additive alan default'lu.

**Not — Storefront "push-only" nüansı:** Storefront normalde dışarı olay yaymaz (push-only). Bu tek türev-event
istisnası plan Constitution Check'te gerekçelenir: öznitelikleri zaten tutuyor (en ucuz enrichment noktası),
Order→Catalog kuplajından kaçınılır; yayılan bir **event**tir (senkron çağrı değil), izolasyon korunur.