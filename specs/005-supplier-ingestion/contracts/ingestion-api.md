# Contract: Ingestion API (IngestionAgent)

URL-segment sürümleme (`v1`). Tüm uçlar şimdilik anonimdir (kullanıcı kararı, 2026-07-22); domain yazımları
agent'ların M2M token'ıyla zaten scope korumalıdır. Yanıtlar mevcut Result zarfıyla döner (`IsSuccess`, `Data`, `Messages`).

## POST /v1/ingestion/runs — aktarım tetikle (FR-007)

- Auth: yok (anonim); ileride `.RequireAuthorization` ile scope eklenebilir.
- `202 Accepted` → `{ "runId": "guid" }`; run arka planda koşar.
- `409 Conflict` → hâlihazırda bir run sürüyorsa (FR-024, süreç içi kilit).

## GET /v1/ingestion/runs — run listesi

- Anonim. Son run'lar, yeniden eskiye; `status`, başlangıç/bitiş ve toplam sayaç özetiyle.

## GET /v1/ingestion/runs/{id} — run özeti (FR-022)

- Anonim. `404` bilinmeyen id.

```json
{
  "id": "…", "status": "Completed", "startedAtUtc": "…", "finishedAtUtc": "…",
  "suppliers": [
    { "supplierCode": "acme",   "fetchStatus": "Fetched",     "new": 10, "updated": 2, "skipped": 40, "failed": 1 },
    { "supplierCode": "nordic", "fetchStatus": "Unreachable", "new": 0,  "updated": 0, "skipped": 0,  "failed": 0 },
    { "supplierCode": "tekno",  "fetchStatus": "Empty",       "new": 0,  "updated": 0, "skipped": 0,  "failed": 0 }
  ]
}
```

## GET /v1/ingestion/staging — ara kayıt listesi (FR-023)

- Anonim. Query: `status?` (`Pending|Processing|Completed|Failed`), `supplier?`, `page?`, `pageSize?`.
- Yanıt sayfalıdır; kayıt başına özet: id, supplierCode, externalId, status, errorReason, processedAtUtc.

## GET /v1/ingestion/staging/{id} — tekil ara kayıt (SC-005)

- Anonim. `id` = `{supplierCode}:{externalId}`. `404` bilinmeyen id.
- Yanıt tam dokümandır: `rawPayload` (telden gelen ham hali) + `normalized` + hash + durum + hata nedeni.

## Davranış notları

- Hatalı (Failed) kayıtlar bir sonraki run'da otomatik yeniden denenir (FR-021); ayrı bir retry ucu yoktur.
- Run sürerken staging okumaları serbesttir; kayıtlar `Processing` durumuyla görünebilir.