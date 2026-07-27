# Quickstart: Canlı Doğrulama (015)

**Date**: 2026-07-27 | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/writer-agents.md](contracts/writer-agents.md)

## Önkoşullar

- `dotnet user-secrets set "OpenAI:ApiKey" "<key>" --project src/agents/IngestionAgent/IngestionAgent.csproj`
- `dotnet user-secrets set "OpenAI:Model" "<model>" --project src/agents/IngestionAgent/IngestionAgent.csproj`
- `dotnet build` temiz; `dotnet test` yeşil.
- Sistem: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (her zaman Aspire üzerinden).

## Senaryo 1 — Başarılı LLM-sürücülü akış (US1)

1. Supplier.Gateway Hangfire panosundan (dev-only) pull job'ını tetikle veya cron'u bekle.
2. ingestion-agent loglarında sırayla catalog → stock → discount LLM tool çağrılarını gör.
3. Catalog/Stock/Discount durumunun snapshot'ı yansıttığını doğrula (Scalar/WebApp).
4. İndirimsiz kayıtta `remove_product_discount` çağrıldığını ve başarı döndüğünü gör.

**Beklenen**: değişen kayıt başına en çok 3 model-sürücülü adım; değişmeyen kayıt 0 (SC-006).

## Senaryo 2 — Hata / retry / DLQ (US2)

1. Aspire panosundan `stock-api`'yi durdur; feed'de stoklu bir değişiklik tetikle.
2. Logda: catalog OK, stock FAIL; discount adımı hiç koşmadı (LLM çağrısı da yok — SC-003).
3. Retry'ları izle (10s/30s/60s); tükenince RabbitMQ management'ta DLQ'da mesajı gör.
4. DLQ mesajında kayıt kimliği (ExternalId) + hata kodunun taşındığını doğrula (SC-002).
5. `stock-api`'yi başlat; DLQ mesajını kuyruğa geri taşı → tam replay yakınsar; catalog "updated" döner (SC-004).

## Senaryo 3 — Temizlik / tekdüzelik (US3)

1. `grep -rn "ToolOutcome\|ToolResponse\|ToolMessage\|ToolData" src/agents/IngestionAgent` → 0 sonuç (SC-005).
2. Üç executor'ın da `WriterResult` sözleşmesini kullandığını doğrula.
3. `dotnet test tests/IngestionAgent.Tests/IngestionAgent.Tests.csproj` yeşil.

## Senaryo 4 — Fail-fast config (FR-014)

1. `OpenAI:ApiKey` secret'ını geçici kaldır → ingestion-agent açılışta hata verir (Aspire panosunda görünür).
2. Secret'ı geri ekle → normal açılış.