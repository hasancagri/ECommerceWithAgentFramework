# REST API Contract — Customer.Api (v1)

Base: `/api/v1` (URL-segment sürümleme). Auth: JWT bearer. Kullanıcı `CurrentUser.Load(...)`
ile token'dan çözülür; body/route `UserId` taşımaz. Yanıt zarfı: `FeatureResultModel` /
`FeatureObjectResultModel<T>` / `FeatureListResultModel<T>` (`IsSuccess` → Ok/BadRequest).

## AddressBook — `/api/v1/addresses`

| Metot | Yol | Scope | Gövde | Sonuç |
|-------|-----|-------|-------|-------|
| GET | `/addresses` | `customer.read` | — | `FeatureListResultModel<AddressView>` (boş=NotFound zarfı) |
| POST | `/addresses` | `customer.write` | `AddressInput` | `FeatureObjectResultModel<{Id}>` |
| PUT | `/addresses/{id}` | `customer.write` | `AddressInput` | `FeatureResultModel` |
| DELETE | `/addresses/{id}` | `customer.write` | — | `FeatureResultModel` |
| POST | `/addresses/{id}/default` | `customer.write` | — | `FeatureResultModel` |

**AddressInput**: `{ province, district, street, zipCode, line }` — zorunlu alanlar boşsa Error.

**AddressView**: `{ id, province, district, street, zipCode, line, isDefault }`.

## Wallet — `/api/v1/cards`

| Metot | Yol | Scope | Gövde | Sonuç |
|-------|-----|-------|-------|-------|
| GET | `/cards` | `customer.read` | — | `FeatureListResultModel<CardView>` |
| POST | `/cards` | `customer.write` | `AddCardInput` | `FeatureObjectResultModel<{Id}>` |
| DELETE | `/cards/{id}` | `customer.write` | — | `FeatureResultModel` |
| POST | `/cards/{id}/default` | `customer.write` | — | `FeatureResultModel` |

**AddCardInput**: `{ pan, cvv, expiryMonth, expiryYear, label? }`
- **PAN/CVV yalnız istekte**; tokenize'a geçer, saklanmaz/loglanmaz (FR-008).
- Tokenize başarısız/geçmiş son-kullanma → Error, kayıt yok (FR-009/013).
- Kart **update yok** (sil + yeniden ekle).

**CardView**: `{ id, brand, last4, expiryMonth, expiryYear, label, isDefault }`
- **Token ve PAN/CVV hiçbir koşulda dönmez** (SC-002).

## Hata semantiği

- Doğrulama/iş kuralı → `IsSuccess=false`, `Messages[].Code` = resource sabiti; HTTP 400.
- Yetki: scope yoksa 403; kimlik yoksa 401. Başka UserId kaydına erişim → NotFound/403 (kayıt
  sorgusu zaten `UserId` ile filtreli; sızıntı yok).