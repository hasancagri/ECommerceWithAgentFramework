# Data Model: Davranış-Bazlı Kişiselleştirme (042)

Tek sahip: Python Personalization servisi (`personalizationDb`, şema `personalization`).
Marten YOK (BC .NET değil); şema init servisin açılışında `CREATE TABLE IF NOT EXISTS` ile.
.NET tarafında kalıcı model YOK — WebApp yalnız `BehaviorEvent` DTO'sunu satıra serileştirir
(kontrat: [contracts/behavior-log-line.md](contracts/behavior-log-line.md)).

## Tablolar

### behavior_events — ham davranış kaydı (spec: BehaviorEvent)

| Kolon | Tip | Not |
|-------|-----|-----|
| id | bigserial PK | iç anahtar |
| event_type | text NOT NULL | ProductViewed / ListShown / SearchPerformed / BasketItemAdded |
| channel | text NOT NULL | şimdilik hep `web` |
| user_id | uuid NULL | login'li kullanıcı |
| anonymous_id | uuid NOT NULL | çerez kimliği |
| product_id | uuid NULL | view/add'de dolu |
| brand | text NULL | view/add'de dolu (yakalama anında denormalize) |
| category | text NULL | view/add'de dolu |
| price | numeric(18,2) NULL | view/add'de dolu |
| search_term | text NULL | yalnız SearchPerformed |
| shown_product_ids | uuid[] NULL | yalnız ListShown (impression) |
| session_id | uuid NOT NULL | oturum grubu (spec: Session — ayrı tablo YOK, gruplama alanı) |
| occurred_at | timestamptz NOT NULL | satırdaki Timestamp |
| schema_version | int NOT NULL | kontrat sürümü (v1) |
| source_file | text NOT NULL | idempotency: kaynak dosya adı |
| line_no | int NOT NULL | idempotency: dosyadaki satır no |

- UNIQUE (source_file, line_no) — çift kayıt imkânsız (FR-009, SC-006).
- INDEX (user_id), (anonymous_id), (occurred_at) — eğitim + popüler sorguları.
- Doğrulama (ingest'te): event_type bilinmiyorsa veya zorunlu alan boşsa satır atlanır (FR-010).

### ingest_offsets — okuma ilerlemesi

| Kolon | Tip | Not |
|-------|-----|-----|
| file_name | text PK | behavior-YYYYMMDD.jsonl |
| byte_offset | bigint NOT NULL | işlenen son konum |
| line_count | int NOT NULL | işlenen satır sayısı |
| skipped_count | int NOT NULL | atlanan (bozuk) satır sayısı — gözlemlenebilirlik (FR-010) |
| updated_at | timestamptz NOT NULL | |

- INSERT(behavior_events) + UPDATE(ingest_offsets) aynı transaction'da (R4).

### model_runs — eğitim meta (spec: TrainedModel)

| Kolon | Tip | Not |
|-------|-----|-----|
| id | bigserial PK | |
| trained_at | timestamptz NOT NULL | |
| event_count | bigint NOT NULL | eğitime giren etkileşim sayısı |
| user_count / item_count | int NOT NULL | matris boyutları |
| model_path | text NOT NULL | .npz dosya yolu |
| status | text NOT NULL | Succeeded / Failed / SkippedNoData |

- Model dosyasının kendisi diskte (`models/als-<runId>.npz`, atomik tmp+rename; R5).
- Süreç açılışta en son Succeeded run'ın dosyasını yükler.

### popular_products — fallback listesi

| Kolon | Tip | Not |
|-------|-----|-----|
| rank | int PK | 1..50 |
| product_id | uuid NOT NULL | |
| view_count | bigint NOT NULL | son 7 gün ProductViewed sayısı |
| computed_at | timestamptz NOT NULL | |

- Eğitim job'ı her koşuda TRUNCATE + yeniden yazar (R5). Boşsa sorgu yine boş dönmez:
  fallback zinciri model → popüler → (hiç veri yoksa) boş liste yalnız depo tamamen boşken (spec
  edge: bu durum yalnız hiç gezinti yokken mümkündür).

## Bellek-içi yapılar (kalıcı değil)

- **AlsModel**: user_factors, item_factors, user_index (kimlik→satır), item_index (ürün→sütun).
  Tek atomik referans; eğitim sonrası swap (FR-011). (spec: TrainedModel'in çalışan hali)
- **RecommendationResult** (API yanıtı): product_ids sıralı liste + source (`personal` |
  `session` | `popular`) — kontrat: [contracts/recommendations-api.md](contracts/recommendations-api.md).

## İlişki özeti

```
JSONL satırı ──ingest──► behavior_events ──eğitim──► model_runs + .npz + popular_products
                                                        │
GET /recommendations ──► AlsModel (RAM) ── fallback ──► popular_products
```