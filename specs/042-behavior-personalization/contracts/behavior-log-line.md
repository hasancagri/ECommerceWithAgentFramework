# Kontrat: Davranış Log Satırı (JSONL v1)

Üretici: WebApp `BehaviorLogWriter`. Tüketici: Personalization `ingest.py`.
Dosya: `<BEHAVIOR_LOG_DIR>/behavior-YYYYMMDD.jsonl` (UTC gün; yalnız bugünün dosyasına eklenir,
eski dosyalar değişmez). Her satır = tek JSON nesnesi, UTF-8, `\n` sonlu.

## Alanlar

| Alan | Tip | Zorunlu | Not |
|------|-----|---------|-----|
| eventType | string | ✔ | `ProductViewed` \| `ListShown` \| `SearchPerformed` \| `BasketItemAdded` |
| channel | string | ✔ | v1'de hep `web` |
| userId | guid | – | login'li kullanıcıda dolu |
| anonymousId | guid | ✔ | `pz_aid` çerezi |
| productId | guid | koşullu | ProductViewed + BasketItemAdded'da zorunlu |
| brand | string | koşullu | ProductViewed + BasketItemAdded'da dolu (yakalama anında denormalize) |
| category | string | koşullu | ProductViewed + BasketItemAdded'da dolu (primary kategori adı) |
| price | decimal | koşullu | ProductViewed + BasketItemAdded'da dolu |
| searchTerm | string | koşullu | yalnız SearchPerformed'da, zorunlu |
| shownProductIds | guid[] | koşullu | yalnız ListShown'da, zorunlu, boş olamaz |
| sessionId | guid | ✔ | `pz_sid` çerezi |
| timestamp | string (ISO-8601 UTC) | ✔ | yakalama anı |
| schemaVersion | int | ✔ | bu kontrat = `1` |

## Kurallar

- Bilinmeyen alan: tüketici YOK SAYAR (ileri uyumluluk). Bilinmeyen `eventType` veya eksik zorunlu
  alan: satır atlanır, `skipped_count` artar — ingest DURMAZ (FR-010).
- `schemaVersion` ≠ 1: satır atlanır (gelecek sürüm tüketicisi bilmiyorsa sessiz geçiş).
- Kişisel veri (ad, e-posta, adres, demografi) HİÇBİR alanda taşınmaz (FR-007).
- Alan adları camelCase; JSON'da null yerine alan hiç yazılmaz (kompakt satır).

## Örnekler

```json
{"eventType":"ProductViewed","channel":"web","userId":"6f1e...","anonymousId":"a3b2...","productId":"9c8d...","brand":"Acme","category":"Telefon","price":18999.90,"sessionId":"5e4f...","timestamp":"2026-08-21T14:03:22.512Z","schemaVersion":1}
{"eventType":"ListShown","channel":"web","anonymousId":"a3b2...","shownProductIds":["9c8d...","7b6a..."],"sessionId":"5e4f...","timestamp":"2026-08-21T14:03:25.104Z","schemaVersion":1}
{"eventType":"SearchPerformed","channel":"web","anonymousId":"a3b2...","searchTerm":"kablosuz kulaklık","sessionId":"5e4f...","timestamp":"2026-08-21T14:04:01.330Z","schemaVersion":1}
{"eventType":"BasketItemAdded","channel":"web","userId":"6f1e...","anonymousId":"a3b2...","productId":"9c8d...","brand":"Acme","category":"Telefon","price":18999.90,"sessionId":"5e4f...","timestamp":"2026-08-21T14:05:44.907Z","schemaVersion":1}
```

## Evrim

- Alan ekleme = minor (schemaVersion sabit kalır, tüketici bilmediğini yok sayar).
- Alan anlamı/tipi değişimi veya zorunluluk değişimi = `schemaVersion` artar; tüketici yeni sürümü
  tanıyana dek satırları atlar (veri kaybı kabul — kayıp-toleranslı telemetri).
- İkinci tüketici doğarsa bu kontrat integration event'e terfi eder (R7).