# Kontrat: ReviewSummaryChanged Integration Event (044)

`Shared.IntegrationEvents` — ADDITIVE; mevcut event'ler değişmez. Yayıncı: Reviews.Api.
Tüketici: Storefront.Api. Fanout exchange; adlar `RabbitMqConstants`'ta merkezileşir.

## Record

```csharp
public record ReviewSummaryChanged(Guid ProductId, decimal Average, int Count);
```

- `Average`: Visible yorumların ortalaması, Reviews HESAPLAR (tüketici saymaz — R3, SC-003).
  Ondalık taşınır (ör. 4.5); yuvarlama/gösterim UI işi.
- `Count`: Visible yorum adedi. **`Count=0` ⇒ tüketici özeti TEMİZLER** (Average yok sayılır;
  rozet çizilmez — FR-006). Tek yorumlu ürünün yorumu gizlenince bu yol çalışır.

## Yayın anları

- SubmitReview commit sonrası (yeni yorum → özet yükselir/oluşur).
- ModerateReview ihlal kararı commit sonrası (`Hidden` → özet düşer; kalan 0 ise Count=0).
- Temiz moderasyon kararı yayın YAPMAZ (özet değişmedi).

## Taşıma

- Fanout exchange: `reviews.summary-changed` (RabbitMqConstants sabiti).
- Kuyruk: mevcut `storefront.events` TEK-kuyruk deseni — binding'i TÜKETİCİ kurar (041 dersi);
  Sequential işleme aynı satıra eşzamanlı yazımı yapısal olarak engeller.
- Sıra garantisi aranmaz: payload MUTLAK değerdir (delta değil) — geç/yeniden teslim
  son-yazan-kazanır ile güvenli; idempotent upsert yeter.

## Storefront tarafı

- `StorefrontView` += `RatingAverage (decimal?)`, `RatingCount (int)` (R6).
- `ApplyReviewSummary(avg, count)`: Count=0 ⇒ RatingAverage=null + RatingCount=0.
- Handler `StorefrontEventHandlers`'a eklenir (Wolverine keşfi; `IncludeType` —
  bilinen keşif tuzağı). Satır yoksa KISMİ satır yaratılır (diğer kaynaklarla aynı desen;
  dolu-satır filtresi Catalog verisi gelene dek zaten gizler).
- Liste sorgusu `[Cached]` değil (011 K4) ve rating facet'e girmez — invalidation adımı YOK.