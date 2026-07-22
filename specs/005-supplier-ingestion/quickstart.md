# Quickstart: Tedarikçi Entegrasyonu Doğrulama (005)

Uçtan uca doğrulama rehberi. Kontratlar için `contracts/`, model için `data-model.md`.
Bu akışta token hiç kullanılmaz: ingestion uçları da domain yazımları da anonimdir (kullanıcı kararı, 2026-07-22).
Token sistemde yalnız kullanıcı alışveriş akışında (basket/order/payment) kalır.

## Ön koşullar

- `IngestionAgent` için OpenAI anahtarı config'te (`OpenAI:ApiKey`, ChatAgent ile aynı desen).
- `Supplier.Api/Datasets/{acme,nordic,tekno}.json` dolu (şema: `contracts/supplier-feeds.md`).
- Sistem her zaman Aspire ile başlar: `dotnet run --project src/aspire/AppHost/AppHost.csproj`.

## Senaryo 1 — İlk aktarım (US1, SC-001, SC-007)

1. Feed'leri gör: `GET <supplier-api>/v1/feeds/acme` (JSON), `/nordic` (CSV), `/tekno` (XML).
2. Tetikle: `curl -X POST <ingestion>/v1/ingestion/runs` → `202` + runId.
3. Özet: `GET /v1/ingestion/runs/{runId}` → tüm sağlam kayıtlar `new`, tedarikçi kırılımlı.
4. Katalog: `GET <catalog-api>/v1/products` → N ürün, markalar eşlenmiş; stoklar feed adediyle eşit.
5. İndirim: yüzdeli kayıtların ürünlerinde `GET <discount-api>` üzerinden aynı oran görünür.

## Senaryo 2 — İdempotency (US2, SC-002)

1. Aynı run'ı tekrar tetikle; bitmesini bekle.
2. Özet: tüm kayıtlar `skipped`, `new/updated/failed` = 0; katalog/stok/indirim değişmemiş.

## Senaryo 3 — Güncelleme (US3, SC-003)

1. `Datasets/acme.json`'da tek ürünün fiyatını değiştir; Aspire panelinden `supplier-api`'yi yeniden başlat.
   (Simülatör seed'i upsert çalışır — plan kararı; dataset değişikliği restart'ta DB'ye yansır.)
2. Tetikle → özet: 1 `updated`, kalanlar `skipped`; katalogda yalnız o ürünün fiyatı değişmiş.
3. İndirimi silinen kayıt varsa üründeki indirim kalkmış olmalı (FR-026).

## Senaryo 4 — Hatalı kayıt izolasyonu (US4, SC-004)

1. Veri setine boş `brand`'li bir kayıt ekle, yeniden başlat, tetikle.
2. Run `Completed`; özet 1 `failed`. `GET /v1/ingestion/staging?status=Failed` kaydı listeler.
3. `GET /v1/ingestion/staging/{supplier}:{externalId}` → `rawPayload` + `errorReason` görünür (SC-005).
4. Veriyi düzelt, tekrar tetikle → kayıt bu kez işlenir (FR-021).

## Senaryo 5 — Eşzamanlılık (FR-024)

1. Run sürerken ikinci `POST /v1/ingestion/runs` → `409 Conflict`.

## Senaryo 6 — Erişilemeyen tedarikçi (edge)

1. Aspire panelinden `supplier-api`'yi durdurup tetikle.
2. Run biter; özette tedarikçiler `Unreachable`, akış çökmez, run `Completed` kalır.

## Birim testleri

```bash
dotnet test tests/IngestionAgent.Tests/IngestionAgent.Tests.csproj   # adapter, hash, marka, zarf, fark tespiti
dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj             # ProductStock.SetQuantity
```