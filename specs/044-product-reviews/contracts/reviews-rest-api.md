# Kontrat: Reviews REST API (044)

Reviews.Api yüzeyi. URL-segment sürümleme (`v1`); gateway route `/reviews` → Reviews.Api.
Yanıt zarfı her yerde `Feature*ResultModel` (IsSuccess + Messages).

## POST /api/v1/reviews — yorum gönder (SubmitReview)

- Yetki: bearer + `reviews.write` scope. Kullanıcı `CurrentUser.Load` ile çözülür;
  UserId + görünen ad token claim'lerinden (istek gövdesinden ASLA alınmaz).
- Gövde:

```json
{ "productId": "guid", "rating": 4, "text": "opsiyonel, ≤2000" }
```

- Akış: Order gRPC `HasConfirmedPurchase(sub, productId)` → false/erişilemez = RED (fail-closed)
  → `Review.Create` → kaydet → özet hesapla + `ReviewSummaryChanged` yayınla → `reviews.moderate` kuyruğa.
- Başarı `200`: `FeatureObjectResultModel<SubmitReviewResponse>` — `{ reviewId, maskedName, rating }`.
- Hata `400` (`Messages[].Code`, hepsi `ReviewsResourceConstants`):

| Code | Durum |
|---|---|
| REVIEW_PURCHASE_REQUIRED | Confirmed sipariş yok (FR-001) |
| REVIEW_PURCHASE_CHECK_UNAVAILABLE | Order gRPC erişilemez — fail-closed (FR-008) |
| REVIEW_ALREADY_EXISTS | aynı kullanıcı+ürün ikinci yorum (FR-003, unique index yarışı dahil) |
| REVIEW_RATING_INVALID | rating 1-5 tam sayı dışı (FR-002) |
| REVIEW_TEXT_TOO_LONG | metin > 2000 karakter |
| REVIEW_NAME_REQUIRED | token'da görünen ad boş (beklenmez; guard) |

## GET /api/v1/reviews/products/{productId} — yorum listesi (anonim)

- Yetki: YOK (herkese açık, FR-004). Sayfalama: `?page=1&pageSize=10` (max 50).
- Sıra: en yeni üstte (`CreatedTime desc`). `Hidden` yorumlar HARİÇ (sunucuda filtre).
- Başarı `200`: `FeaturePagedResultModel<ProductReviewItem>`:

```json
{ "maskedName": "H** D**", "rating": 4, "text": "…", "createdTime": "2026-08-22T…" }
```

- Ham ad hiçbir yanıtta YOK — yalnız `Masked()` çıktısı (R7). Rozet metni UI işi:
  şart gereği her yorum doğrulanmıştır, ayrı alan taşınmaz (FR-005).
- Boş liste `FeaturePagedResultModel` NotFound davranışına uyar; WebApp "henüz yorum yok" çizer.
- Özet (ortalama+adet) bu uçtan DÖNMEZ — kart/detay özeti StorefrontView'dan gelir (R6).

## GET /api/v1/reviews/products/{productId}/eligibility — form göster/gizle kararı

- Yetki: bearer + `reviews.write`. SC-001 "arayüzde form yok" için: WebApp formu yalnız
  `canReview=true` iken çizer; girişsizde uç hiç çağrılmaz (form zaten yok).
- Başarı `200`: `FeatureObjectResultModel<ReviewEligibilityResponse>`:

```json
{ "canReview": false, "reasonCode": "REVIEW_ALREADY_EXISTS" }
```

- `reasonCode`: null (uygun) | REVIEW_PURCHASE_REQUIRED | REVIEW_ALREADY_EXISTS |
  REVIEW_PURCHASE_CHECK_UNAVAILABLE (gRPC erişilemez — form gizlenir, fail-closed tutarlı).
- Uç karar VERMEZ, öngörü verir; nihai guard her zaman SubmitReview'dadır (yarışta 400).