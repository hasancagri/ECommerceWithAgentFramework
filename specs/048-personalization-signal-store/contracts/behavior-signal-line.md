# Contract: Behavior Signal Body (Gezinme Sinyal Gövdesi)

Tek bir gezinme sinyalinin şema-kontratı. `POST /v1/signals` batch'inin öğesi.
WebApp'in `BehaviorEvent` record'u (Services/Behavior/BehaviorEvent.cs) bu kontratı
karşılar.

> **049 sadeleştirme:** Liste/sonuç sayfası sinyalleri ve ölü alanlar söküldü (kullanıcı
> kararı). Kalan sinyal = yalnız ürün detay ziyareti + sepete ekleme.

## Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| `eventType` | string | ✅ | Bilinen küme (aşağıda) |
| `channel` | string | ✅ (default "web") | Kaynak kanal |
| `userId` | Guid? | ❌ | Giriş yaptıysa (anonim gezinme opsiyonel) |
| `anonymousId` | Guid | ✅ | `pz_aid` cookie (anonim atıf kimliği) |
| `productId` | Guid? | ❌ | ProductViewed / BasketItemAdded |
| `brand` | string? | ❌ | denormalize (görüntüleme anı) |
| `category` | string? | ❌ | denormalize |
| `price` | decimal? | ❌ | denormalize |
| `timestamp` | DateTime (UTC) | ✅ (default now) | client zamanı |

## eventType değerleri

`ProductViewed`, `BasketItemAdded`.

- **Liste/sonuç sayfası sinyalleri KAYDEDİLMEZ** (kullanıcı kararı, 049): `ListShown`,
  `CategoryViewed`, `BrandViewed`, `SearchPerformed` artık REDDEDİLİR (bilinen kümede yok).
- Marka/kategori gerekirse `productId`'den (katalog) türetilir — liste impression'ı saklanmaz.

## PII yasağı

- Ad, e-posta, adres, telefon, kart YOK. Yalnız opak kimlikler (`userId`/`anonymousId`)
  + davranış alanları (FR-012 / SC-005).

## Evrim kuralı

- Tüketici bilinmeyen alanı yok sayar (tolerant read); yeni alan additive + default'lu
  eklenir. (Sürüm alanı `schemaVersion` 049'da söküldü — sadeleştirme.)
