# Implementation Plan: IngestionAgent LLM-Sürücülü Yazıcılar

**Branch**: `015-ingestion-llm-writers` | **Date**: 2026-07-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-ingestion-llm-writers/spec.md`

## Summary

- Deterministik MCP çağrı makinesi (`McpToolInvoker` + zarf ayna tipleri) silinir.
- Üç yazıcı adım, ChatAgent'ın `ChatClientAgent` + MCP-tool-as-`AIFunction` deseninin **anonim** kopyasıyla LLM-sürücülü olur.
- Her adım tipli `WriterResult` döner (structured output); elle zarf parse kalkar.
- MAF workflow kalır; short-circuit conditional edge + terminal collector'a taşınır (önce spike — FR-015).
- Dış davranış değişmez: kuyruk/DLQ adları, retry, `IngestionWriteException` köprüsü, `WORKFLOW_INCOMPLETE`, idempotent yazmalar korunur.
- Kapsam dışı (gelecek adayı): ingestion sonucunun Gateway'e ack'lenmesi / mutabakat sweep'i — ayrı feature (2026-07-27 tartışması).

## Technical Context

**Language/Version**: .NET 10 / C#

**Primary Dependencies**: Microsoft.Agents.AI 1.13.0, Microsoft.Agents.AI.Workflows 1.13.0,
Microsoft.Extensions.AI(.OpenAI) 10.7.0, ModelContextProtocol.Core 1.4.0, WolverineFx.RabbitMQ 6.4.1

**Storage**: yok — IngestionAgent DB'siz/state'siz kalır (007 duruşu).

**Testing**: xUnit + Shouldly; saf birim testleri (WriterResult sözleşmesi, kısa-devre koşulu). Host/entegrasyon harness'ı yok.

**Target Platform**: Aspire içi background servis (`ingestion-agent` resource'u).

**Project Type**: mevcut tek projenin (src/agents/IngestionAgent) yerinde refactor'u; yeni servis/DB/endpoint yok.

**Performance Goals**: hacim düşük (diff-only feed); kayıt başına birkaç saniyelik LLM gecikmesi kabul (spec Assumptions).

**Constraints**: adım bütçesi (LLM+tool) mesaj işleme penceresine sığmalı; yazma yolu anonim; tüm yazmalar idempotent.

**Scale/Scope**: 3 yazıcı agent, 4 MCP tool, 1 kuyruk; kod ayak izi tek proje + küçük test projesi.

## Constitution Check

*GATE: Phase 0 öncesi geçildi; Phase 1 sonrası yeniden değerlendirildi — sonuç değişmedi.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC izolasyonu | PASS | İletişim yalnız MCP (sanksiyonlu kanal); DB/tablo erişimi yok. |
| II. Zengin aggregate | N/A | IngestionAgent'ta domain modeli yok; aggregate'lere mevcut tool'lar üzerinden yazılır. |
| III. Slice + CQRS | PASS | Servis API değil; çağrılan Agent slice'ları ve MCP tool imzaları değişmez. |
| IV. Result pattern | PASS | Adımlar tipli `WriterResult` döner; exception yalnız Wolverine ack/nack köprüsü (mevcut desen). |
| V. Scope yetki | PASS | Yazma yolu anonim kalır (FR-008); rol yok, yeni scope yok. |
| Teknoloji kısıtları | PASS | Paket sürümleri CPM'de mevcut; agent tipleri Singleton; sistem Aspire'dan çalışır. |

## Project Structure

### Documentation (this feature)

```text
specs/015-ingestion-llm-writers/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 kararları (R1–R8)
├── data-model.md        # Snapshot, RecordJob, WriterResult
├── quickstart.md        # Canlı doğrulama senaryoları
├── contracts/
│   └── writer-agents.md # Agent/tool/çıktı/config sözleşmeleri
└── tasks.md             # /speckit-tasks üretecek
```

### Source Code (repository root)

```text
src/agents/IngestionAgent/
├── Program.cs                      # DEĞİŞİR — OpenAI fail-fast, IChatClient, 3 yazıcı agent kaydı
├── Infrastructure/
│   ├── HttpClients.cs              # KALIR — mcp-no-token named client
│   ├── IngestionWriteException.cs  # KALIR — Wolverine retry/DLQ köprüsü
│   ├── AnonymousMcpTool.cs         # YENİ — AIFunction sarmalayıcı (PerUserMcpTool'un token'sız hali)
│   └── McpToolCatalog.cs           # YENİ — lazy discovery + allowlist → AITool listesi
├── Workflows/
│   ├── SupplierSnapshotHandler.cs  # DEĞİŞİR — conditional edge + terminal collector
│   ├── RecordJob.cs                # KALIR — Message/ProductId/Failure/Completed akışı
│   ├── WriterResult.cs             # YENİ — tipli sonuç sözleşmesi
│   ├── 01_CatalogWrite/            # DEĞİŞİR — LLM-sürücülü executor + agent
│   ├── 02_StockWrite/              # DEĞİŞİR — 03'ten yeniden numaralanır (akış sırası)
│   └── 03_DiscountWrite/           # DEĞİŞİR — 02'den yeniden numaralanır
└── SİLİNİR: Infrastructure/McpConnector.cs, Infrastructure/McpToolInvoker.cs
            (ToolData/ToolMessage/ToolResponse/ToolOutcome zarf ayna tipleri dahil)

tests/IngestionAgent.Tests/         # YENİ — WriterResult sözleşme + kısa-devre birim testleri
```

**Structure Decision**: tek mevcut proje yerinde refactor edilir; klasör numaraları gerçek akış
sırasına (Catalog → Stock → Discount) getirilir. Yeni proje yalnız test projesidir.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Deterministik yazma yoluna LLM girer | MCP'yi gerçekten LLM'e kullandırmak + ham-feed normalizasyonu iskeleti | 007 "NO LLM" duruşu bilinçli ürün kararıyla tersine çevrildi (spec Assumptions; kullanıcı onayı 2026-07-27) |