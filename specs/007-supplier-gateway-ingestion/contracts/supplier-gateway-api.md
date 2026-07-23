# Contract: Supplier.Gateway HTTP API

Tümü anonim (005 kararı: token yalnız alışveriş akışında). Sürümleme URL-segment (`v1`).

## POST /v1/feeds/pull — manuel çekim tetiği

Periyodik zamanlayıcıyla aynı kapıdan (tek-çekim kilidi) geçer.

| Durum | Cevap |
|-------|-------|
| Çekim başlatıldı | `202 Accepted` — gövde: `{ "started": true }` |
| Çekim zaten sürüyor | `409 Conflict` — gövde: `{ "error": "PULL_ALREADY_IN_PROGRESS" }` |

Cevap çekimin SONUCUNU taşımaz (fire-and-forget); sonuç log + kuyruk/DLQ üzerinden gözlemlenir.

## Kaldırılan API (IngestionAgent)

`/v1/ingestion/runs*` uçları siliniyor; beslendiği IngestionRun modeli ölüyor.
Yerine geçen görünürlük: RabbitMQ management UI (kuyruk derinliği + DLQ) ve servis logları.