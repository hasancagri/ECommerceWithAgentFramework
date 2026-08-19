# Implementation Plan: Multi-Supplier Dropship — Procurement BC (Havuz + Buy-Box)

**Branch**: `041-multi-supplier-buybox` | **Date**: 2026-08-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/041-multi-supplier-buybox/spec.md`

## Summary

Yeni **Procurement BC**: iki mock tedarikçi feed'ini adapter'la çeker, ham satırları barkod-anahtarlı havuzda toplar
(kalıcı, hash-diff'li), kanonik içeriği deterministik birleştirir, eksikleri in-process enrich agent (OpenAI) tamamlar,
eksiksiz ürünü fat event'le Catalog'a yayınlar ve buy-box (stoklu en ucuz) kararını `BuyBoxChanged` ile duyurur.
Catalog fiyatı günceller + `ProductLinked` yayınlar; Stock kazananın stoğunu mutlak yazar. Supplier.Gateway ve
IngestionAgent (015 LLM zinciri) tamamen sökülür.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten 9.5.0 (doküman store), Wolverine 6.4.1 (bus + RabbitMQ), Hangfire 1.8.24 (feed cron),
Microsoft.Extensions.AI.OpenAI 10.7.0 + Microsoft.Agents.AI (enrich agent), Aspire (orkestrasyon)

**Storage**: Yeni Postgres DB `procurementDb`, Marten şeması `procurementManagement` (SchemaConstants'a eklenir)

**Testing**: xUnit + Shouldly; saf domain birim testleri (PoolProduct/BuyBox/merge test-first — İlke VI)

**Target Platform**: Aspire AppHost altında Linux/macOS dev ortamı

**Project Type**: Mikroservis (yeni BC) + mevcut servislerde event tüketimi + iki proje sökümü

**Performance Goals**: 3500 feed satırı/pull işlenir; enrich yalnız eksik satırda (~%10); tekrar pull sıfır yayın

**Constraints**: AI yapısal yolda SIFIR çağrı; barkod/ölçü AI'dan ASLA; saga yok; onay ekranı yok

**Scale/Scope**: 2 tedarikçi, 3000 benzersiz barkod, 500 çakışma; 1 yeni servis, 2 söküm, 3 yeni event kontratı

## Constitution Check

*GATE: v1.8.1'e karşı değerlendirildi — geçti (aşağıdaki notlarla).*

- **İlke I (BC izolasyonu)**: Procurement kendi DB/şema/modeliyle yeni BC. Servisler-arası yalnız fanout event
  (`CanonicalProductUpserted`, `BuyBoxChanged`, `ProductLinked` → `Shared.IntegrationEvents`). DB paylaşımı yok,
  senkron RPC yok. Kanonik taksonomi adları iki BC'de AYRI seed edilir (bilinçli tekrar; ad = event sözleşmesi). ✅
- **İlke I (MCP yalnız agent)**: enrich agent MCP TÜKETMEZ (in-process LLM, kendi BC verisi); Catalog/Stock yazımı
  event handler'la olur. IngestionAgent MCP zinciri sökülür → MCP yüzeyi küçülür (upsert/set_stock tool'ları gider). ✅
- **İlke II (zengin aggregate)**: `Supplier` ve `PoolProduct` zengin aggregate; buy-box + merge + durum makinesi
  aggregate metotlarında. Koleksiyonlar private + IReadOnlyList. ✅
- **İlke III (VSA + CQRS, repo yok)**: pull/enrich `Features/Commands/`, okumalar `Features/Queries/`;
  handler'lar IDocumentSession, `[Transactional]`; endpoint minimal API. ✅
- **CLAUDE.md "her aggregate REST penceresi" kuralı (2026-08-19)**: PoolProduct/Supplier mutator'ları
  Hangfire pull + enrich kuyruğu SAHİPLİ → kural istisnası, REST'e açılmaz. Okuma pencereleri açılır:
  `GET /v1/suppliers`, `GET /v1/pool-products/{barcode}` (+durum filtreli liste). ✅
- **İlke IV (Result)**: aggregate metotları `ResultDomain`; hata kodları `Procurement.Api/Constants`. ✅
- **İlke V (scope)**: dış yüzey yalnız manuel pull ucu (dev aracı, Gateway emsaliyle anonim) + Hangfire pano (dev).
  Kullanıcıya dönük uç yok; scope eklenmez. Mock Supplier.Api anonim kalır. ✅
- **İlke VI (Domain-TDD)**: PoolProduct davranışları (listing upsert/hash, merge, buy-box, enrich-apply, delist)
  test-first; tasks.md'de test task'ları implementasyondan önce. ✅
- **Artefakt ölçekleme**: Tam kademe — plan/research/data-model/contracts/quickstart üretilir. ✅

## Project Structure

### Documentation (this feature)

```text
specs/041-multi-supplier-buybox/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 kararları
├── data-model.md        # Phase 1 — aggregate/VO/event modeli
├── quickstart.md        # Phase 1 — canlı doğrulama rehberi
├── contracts/
│   ├── integration-events.md   # Yeni/sökülen event + exchange/queue sözleşmeleri
│   └── mock-feed-api.md        # Supplier.Api mock feed sözleşmesi
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/procurement/Procurement.Api/          # YENİ BC
├── Program.cs                                     # Marten+Wolverine+RabbitMQ+Hangfire+OpenAI fail-fast
├── GlobalUsings.cs
├── Constants/ProcurementResourceConstants.cs
├── Options/                                       # FeedPullOptions, EnrichmentOptions (Options pattern)
├── Seeding/ProcurementSeedHostedService.cs        # Supplier'lar + kanonik taksonomi kopyası + eşleme tabloları
├── Infrastructure/
│   ├── Feeds/SupplierFeedClient.cs                # HTTP çekici (service discovery)
│   ├── Feeds/FeedPullJob.cs                       # Hangfire cron + manuel tetik (Gateway emsali)
│   └── Enrichment/EnrichmentAgent.cs              # ChatClientAgent, structured output, MCP'siz
└── Domains/
    ├── Suppliers/
    │   ├── Supplier.cs                            # aggregate: Code, Name, Priority + kategori eşleme
    │   ├── SupplierEndpointExtension.cs           # GET /v1/suppliers (okuma penceresi)
    │   ├── Features/Queries/GetSuppliers.cs
    │   └── ValueObjects/SupplierValueObjects.cs   # CategoryMapping VO
    └── PoolProducts/
        ├── PoolProduct.cs                         # aggregate: barkod-Id, listing'ler, kanonik, durum, buy-box
        ├── Entities/SupplierListing.cs            # (tedarikçi × barkod) ham satır + fiyat/stok + hash + delist
        ├── ValueObjects/PoolProductValueObjects.cs # CanonicalContent, BuyBoxDecision, RowDimensions
        ├── PoolProductEndpointExtension.cs        # POST /v1/feeds/pull (dev) + GET havuz okuma uçları
        ├── Features/Queries/GetPoolProduct.cs     # GET /v1/pool-products/{barcode} (+durum filtreli liste)
        └── Features/Commands/
            ├── PullSupplierFeed.cs                # feed çek + satırları işle (upsert/diff)
            ├── EnrichPoolProduct.cs               # eksik alan tamamlama (lokal durable kuyruk)
            └── PublishPoolProduct.cs              # eksiksiz kanonik + buy-box yayını

src/services/supplier/Supplier.Api/                # MOCK genişler
└── Domains/Feeds/
    ├── FeedGenerator.cs                           # deterministik sabit-seed üretim (rev destekli)
    └── FeedEndpointExtension.cs                   # GET /v1/feeds/{supplier} + POST /v1/feeds/{supplier}/advance

src/services/catalog/Catalog.Api/                  # tüketici + yayın
├── ProcurementEventHandlers.cs                    # CanonicalProductUpserted + BuyBoxChanged → Product yaz
├── Seeding/CatalogTaxonomySeedHostedService.cs    # kanonik Category>SubCategory ağacı
└── Domains/.../Features/Agents/ (SÖKÜM)          # UpsertBrand/UpsertCategory/UpsertProduct slice+tool gider

src/services/stock/Stock.Api/                      # tüketici
├── ProcurementEventHandlers.cs                    # ProductLinked + BuyBoxChanged → OnHand mutlak yaz
└── Domains/Stocks/BarcodeLink.cs                  # barkod↔ProductId eşleme dokümanı
    (Features/Agents/SetStock.cs SÖKÜM)

SÖKÜLENLER: src/services/supplier/Supplier.Gateway (tamamı), src/agents/IngestionAgent (tamamı),
Shared.IntegrationEvents.SupplierProductSnapshotReceived, RabbitMqConstants.SupplierProductSnapshot,
AppHost supplier-gateway + ingestion-agent kayıtları + supplierGatewayDb.

tests/Procurement.Api.Tests/                       # YENİ — saf domain birim testleri (test-first)
```

**Structure Decision**: BC = mikroservis kuralı; Procurement `src/services/procurement` altında tek yeni proje.
Havuz + offer + buy-box + enrich tek BC içinde (clarify kararı); Gateway/IngestionAgent işlevleri içeri katlanır.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Spec'teki `SupplierOffer` ayrı aggregate değil, `PoolProduct` içinde `SupplierListing` entity | Offer=ham satırın fiyat/stok yüzü; buy-box tek aggregate'te atomik hesaplanır | Ayrı aggregate aynı verinin kopyası + cross-aggregate okuma/tutarlılık yükü doğurur |
| Kanonik taksonomi adları iki BC'de tekrar seed edilir | BC izolasyonu — Catalog kendi Category'sini, Procurement eşleme hedefini bilir | Paylaşılan taksonomi sabiti = ortak domain modeli sızıntısı (İlke I ihlali) |