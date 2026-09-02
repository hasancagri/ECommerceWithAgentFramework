# Kontrat: Library.Api REST (060)

Tek tüketici: WebApp BFF (Refit + `AuthenticatedHttpClientHandler`, doğrudan service discovery — gateway route YOK).
Sürümleme URL-segment; tüm uçlar login ister (anonim düğme WebApp'te login'e yönlenir).

## Scope'lar (KnownScopes registry'ye eklenir)

- `library.read` — alarm durumu sorgulama.
- `library.write` — alarm kurma/kaldırma.
- İkisi de `BffServiceScopes`'a girer (WebApp token'ı taşır); rol→scope map'te `customer` demetine admin ekranından eklenir.

## Endpoint'ler

### POST `/api/v1/library/price-alarms`  (`library.write`)

```json
{ "productId": "guid", "productName": "string", "currentPrice": 123.45, "email": "string" }
```

- Kullanıcı `CurrentUser.Load(...)` ile token'dan; `email` WebApp cookie claim'inden (R3 snapshot).
- Aynı ürüne mevcut alarm varsa idempotent `Ok` (ikinci kayıt yazılmaz — FR-002).
- Dönüş: `FeatureResultModel`.

### DELETE `/api/v1/library/price-alarms/{productId}`  (`library.write`)

- Kullanıcının o ürüne alarmı silinir (hard delete). Yoksa `NotFound` (resource sabitiyle).
- Dönüş: `FeatureResultModel`.

### GET `/api/v1/library/price-alarms/{productId}`  (`library.read`)

- Detay sayfası düğme durumu: `{ "exists": true|false }`.
- Dönüş: `FeatureObjectResultModel<Response>`.

## Hata kodları

`Library.Api/Constants/LibraryResourceConstants.cs` — ör. `PriceAlarmNotFound`, `PriceAlarmInvalid`. Serbest metin yok (İLKE IV).

## WebApp tarafı

- Yeni Refit interface `ILibraryRefitService` + `Program.cs` kaydı (`http://library-api`, `AuthenticatedHttpClientHandler`).
- Detay sayfası: login'liyse GET ile durum; düğme POST/DELETE page-handler'ları; anonimde `/Auth/SignIn?returnUrl=/products/{id}` (mevcut desen).