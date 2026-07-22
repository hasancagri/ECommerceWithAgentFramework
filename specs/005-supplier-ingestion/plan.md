# Implementation Plan: Tedarikçi Entegrasyonu (Supplier Ingestion)

**Branch**: `005-supplier-ingestion` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-supplier-ingestion/spec.md`

## Summary

Üç sahte tedarikçiyi tek bir simülatör servisi (Supplier.Api) yayınlar: JSON, CSV ve XML feed'leri.
Yeni IngestionAgent uygulaması MAF Workflows ile feed'leri çeker, adapter'larla (ACL) ortak ara modele çevirir.
Kayıtlar ingestionDb'de StagingRecord olarak saklanır; SHA-256 hash kapısı idempotency'yi deterministik sağlar.
Üç yazıcı agent MCP ile domain'e yazar: CatalogAgent ürün, StockAgent stok, DiscountAgent ürün indirimi.
Catalog SeedData tamamen kaldırılır; ürünler artık yalnız tedarikçi verisinden gelir (kullanıcı kararı, 2026-07-22).

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Microsoft.Agents.AI 1.13.0 + .Workflows 1.13.0 (MAF), Microsoft.Extensions.AI (OpenAI),
Marten 9.5.0, Aspire, Duende IdentityServer (mevcut). Yeni NuGet paketi gerekmiyor.

**Storage**: Postgres — `supplierDb`/`supplierManagement` (simülatör), `ingestionDb`/`ingestionManagement` (staging).

**Testing**: xUnit + Shouldly; saf birim testleri (adapter, hash kapısı, marka eşleme, zarf parse, SetQuantity).

**Target Platform**: Aspire AppHost üzerinden lokal dağıtık çalışma (yeni iki resource: supplier-api, ingestion-agent).

**Project Type**: Bir minimal API servisi (simülatör) + bir agent host uygulaması (MAF Workflows).

**Performance Goals**: Feed başına ≤ ~100 kayıt; run süresi LLM çağrılarıyla sınırlı, dakikalar mertebesi kabul.

**Constraints**: İdempotency kararı %100 deterministik kod (FR-014); aynı anda tek run (FR-024); agent yanıtı katı JSON zarf.

**Scale/Scope**: 3 tedarikçi, ~300 kayıt/run; yeni tedarikçi = yalnız adapter + veri seti (SC-006). Veriler temiz/tekdüze.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. BC izolasyonu**: GEÇER. Yeni projeler kendi DB/şemasını alır; domain'e yazım yalnız MCP ile (FR-019).
- **II. Zengin aggregate**: SAPMALAR VAR — gerekçeler Complexity Tracking'te (simülatör DDD'siz, StagingRecord aggregate değil).
  Stock'ta kural aggregate'te korunur: `ProductStock.SetQuantity` davranış metodu eklenir (negatif adet reddi).
- **III. VSA + CQRS**: Catalog/Stock'a eklenen slice'lar desene uyar (SetStock → Features/Commands). Yeni MCP tool'ları
  ince sarmalayıcıdır. IngestionAgent Wolverine'siz — ChatAgent emsali, gerekçe Complexity Tracking'te.
- **IV. Result pattern**: Yeni command'lar Result döner; ingestion'da beklenen hatalar StagingRecord durumu +
  ErrorReason ile taşınır, exception fırlatılmaz.
- **V. Scope-tabanlı yetki**: SAPMA (kullanıcı kararı, 2026-07-22): token yalnız alışveriş akışında (basket/order/payment).
  Katalog/stok/indirim yazımları ve ingestion uçları şimdilik anonim; `ingestion.agent` M2M client iptal.
  Yetki ileride eklenirse scope-tabanlı olur, rol yok (ilkenin özü korunur). Gerekçe Complexity Tracking'te.

**Post-design re-check (Phase 1 sonrası)**: Sapmalar değişmedi; hepsi Complexity Tracking'te gerekçeli. GEÇER.

## Project Structure

### Documentation (this feature)

```text
specs/005-supplier-ingestion/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 kararları
├── data-model.md        # Phase 1 — FeedRecord, StagingRecord, IngestionRun, SupplierProduct
├── quickstart.md        # Phase 1 — uçtan uca doğrulama rehberi
├── contracts/
│   ├── supplier-feeds.md   # 3 feed biçimi (JSON/CSV/XML)
│   ├── mcp-tools.md        # yeni MCP yazma tool'ları + agent zarfları
│   └── ingestion-api.md    # run tetikleme + staging görüntüleme
└── tasks.md             # Phase 2 (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/services/supplier/Supplier.Api/          # YENİ — tedarikçi simülatörü (bilinçli DDD'siz, DB'SİZ — R12)
├── Program.cs                               # minimal API host (Marten yok), in-proc Wolverine
├── Domains/Feeds/
│   ├── SupplierProduct.cs                   # dataset DTO'su (Marten dokümanı değil)
│   ├── FeedEndpointExtension.cs             # /v1/feeds/{acme|nordic} (tekno HTTP'de değil — R17)
│   └── Features/Queries/Get{Acme|Nordic}Feed.cs   # dosya → format render (JSON/CSV)
├── Datasets/{acme,nordic}.json              # kullanıcı verisi (kanonik JSON), istek anında okunur
data/supplier-drops/tekno.xml                # tekno = dosya-bırakma tedarikçisi (R17)

src/agents/IngestionAgent/                   # YENİ — MAF Workflows ingestion uygulaması (Wolverine YOK)
├── Program.cs                               # Marten (ingestionDb), tokensiz named MCP client'lar, workflow DI
├── Workflows/                               # Fetch → Adapter → StagingGate → agent executor'ları → Summary
├── Adapters/                                # ISupplierFeedAdapter + AcmeJson/NordicCsv/TeknoXml (ACL)
├── Agents/                                  # CatalogAgent, StockAgent, DiscountAgent (agent başına tek MCP)
├── Staging/                                 # StagingRecord, IngestionRun, hash kapısı, marka eşleme
└── Api/                                     # POST /v1/ingestion/runs, GET run/staging endpoint'leri

src/services/catalog/Catalog.Api/            # DEĞİŞİR — create_product/update_product MCP tool'ları
│                                            # Infrastructure/SeedData.cs SİLİNİR (Program.cs kaydı dahil)
│                                            # yazma command'larından [RequiredScope] kaldırılır (şimdilik anonim)
src/services/stock/Stock.Api/                # DEĞİŞİR — SetStock command + SetQuantity + set_stock MCP tool
src/services/discount/Discount.Api/          # DEĞİŞİR — set_product_discount + remove_product_discount MCP tool,
│                                            # yazma command'larından [RequiredScope] kaldırılır (şimdilik anonim)
src/others/Shared/.../SchemaConstants.cs     # DEĞİŞİR — Supplier + Ingestion şema adları
src/aspire/AppHost/AppHost.cs                # DEĞİŞİR — supplierDb, ingestionDb, 2 yeni proje resource

tests/IngestionAgent.Tests/                  # YENİ — adapter, hash, marka eşleme, zarf parse, fark tespiti
tests/Stock.Api.Tests/                       # DEĞİŞİR — ProductStock.SetQuantity testleri
```

**Structure Decision**: Simülatör "dış dünya" servisi olarak `src/services/supplier` altında; ingestion bir agent
uygulaması olarak ChatAgent emsaliyle `src/agents` altında. Testler mevcut `tests/` düzenini izler.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Supplier.Api DDD'siz (aggregate yok) | Dış dünyayı simüle eder; bizim domain'imiz değil | Zengin model, sahte veri servisi için tören olurdu |
| IngestionAgent Wolverine'siz | Agent host; bus/handler keşfi gerekmez (ChatAgent emsali) | Wolverine eklemek kullanılmayan altyapı taşırdı |
| StagingRecord aggregate değil | Teknik iz/staging dokümanı (storefront read-model emsali) | Aggregate töreni invariant'sız veriye değer katmaz |
| Üç ayrı yazıcı agent | Kullanıcı kararı: agent başına tek MCP, net sorumluluk | Tek agent üç MCP'yi karıştırır, izole test zorlaşır |
| Yeni servis ama yeni bounded context değil | Simülatör context haritasına "external system" girer | Simülatörü BC saymak sahte bir domain yaratırdı |
| Yazma uçları şimdilik anonim (İlke V sapması) | Kullanıcı kararı: token yalnız alışveriş akışında | M2M client + token handler bu aşamada tören olurdu; scope tek satırla geri eklenebilir |