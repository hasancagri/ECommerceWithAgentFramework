# Contract: Behavior Signal Body (Gezinme Sinyal Gövdesi)

Tek bir gezinme sinyalinin şema-kontratı. `POST /v1/signals` batch'inin öğesi.
WebApp'in mevcut `BehaviorEvent` record'u (Services/Behavior/BehaviorEvent.cs) bu
kontratı ZATEN karşılar — yeniden kullanılır (dosya yerine HTTP gövdesi olur).

## Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| `eventType` | string | ✅ | Bilinen küme (aşağıda) |
| `channel` | string | ✅ (default "web") | Kaynak kanal |
| `userId` | Guid? | ❌ | Giriş yaptıysa |
| `anonymousId` | Guid | ✅ | `pz_aid` cookie |
| `sessionId` | Guid | ✅ | `pz_sid` cookie |
| `productId` | Guid? | ❌ | ProductViewed / BasketItemAdded |
| `brand` | string? | ❌ | denormalize (görüntüleme anı) |
| `category` | string? | ❌ | denormalize |
| `price` | decimal? | ❌ | denormalize |
| `searchTerm` | string? | ❌ | SearchPerformed |
| `shownProductIds` | Guid[]? | ❌ | ListShown |
| `timestamp` | DateTime (UTC) | ✅ (default now) | client zamanı |
| `schemaVersion` | int | ✅ (default 1) | additive evrim |

## eventType değerleri

`ProductViewed`, `ListShown`, `CategoryViewed`, `BrandViewed`, `SearchPerformed`,
`BasketItemAdded`.

- Bu faz WebApp yalnız `ProductViewed`, `ListShown`, `BasketItemAdded` üretir.
- `CategoryViewed`, `BrandViewed`, `SearchPerformed` endpoint/şema tarafından KABUL
  edilir; WebApp enstrümantasyonu sonraki faz (FR-007a).

## PII yasağı

- Ad, e-posta, adres, telefon, kart YOK. Yalnız opak kimlikler (`userId`/`anonymousId`/
  `sessionId`) + davranış alanları (FR-012 / SC-005).

## Evrim kuralı

- Yeni alan **additive + default'lu** eklenir; `schemaVersion` artar. Eski üretici
  (düşük sürüm gövdesi) reddedilmez; eksik alanlar default. Tüketici bilinmeyen alanı
  yok sayar (tolerant read).