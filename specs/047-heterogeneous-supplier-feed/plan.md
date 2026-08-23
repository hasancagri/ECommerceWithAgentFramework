# Implementation Plan: Heterogeneous Supplier Feed (ACL) + Buy-box Teardown

**Branch**: `047-heterogeneous-supplier-feed` | **Date**: 2026-08-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/047-heterogeneous-supplier-feed/spec.md`

## Summary

İki iş bir feature'da. **(1) Heterojen feed + ACL:** Supplier.Api tek process kalır ama her tedarikçi
KENDİ route'undan KENDİ farklı JSON şeklini döndürür (supplier-a `barcode/price/stock`, supplier-b
`gtin/title/cost/warehouseQty`). Procurement'ta tedarikçi-başı ince **Anti-Corruption adapter** ham
şekli nötr iç `SupplierFeedRowDto`'ya normalize eder; `SupplierFeedClient`'in tek-DTO varsayımı kalkar.
Adres per-supplier config'ten. **(2) Buy-box tam söküm:** barkod global tekil → barkod-başı tek tedarikçi.
PoolProduct çoklu-listing/priority-merge/`EvaluateBuyBox` sadeleşir tek-listing'e; `BuyBoxChanged` olayı
+ Catalog/Stock handler'ları silinir; fiyat/stok tek kanal `CanonicalProductUpserted`'a biner (Stock
onu yeni tüketir). `ProductLinked` + barkod→ürün kimliği sabit kalır.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten 9.x (doküman store), Wolverine 6.x (bus + RabbitMQ fanout), Hangfire
(feed cron), Aspire (orkestrasyon + service discovery). AI dokunulmaz (enrich zinciri aynen kalır).

**Storage**: procurementDb (PoolProduct şeması), catalogDb, stockDb — şema değişmez (döküman store,
alan söküm/ekleme runtime); yeni tablo yok.

**Testing**: xUnit + Shouldly; PoolProduct saf-domain testleri test-first (İlke VI) — tek-listing merge,
delist, publish-kararı. Adapter/endpoint = test-sonrası + canlı (quickstart).

**Target Platform**: Aspire AppHost altında Linux/macOS dev.

**Project Type**: Mevcut 3 BC'de değişiklik (Supplier.Api mock, Procurement, Catalog, Stock) + paylaşılan
kontrat sadeleştirme. Yeni servis/BC yok.

**Performance Goals**: Çekiş davranışı korunur (3000+ satır/pull, tekrar-pull sıfır yayın); adapter
normalize O(n) ham satır; söküm event hacmini AZALTIR (ayrı buy-box event'i kalkar).

**Constraints**: Datasetler ELLE (kod-gen mock yok); barkod global tekil (guard implementasyonu KAPSAM
DIŞI); AI yapısal yolda sıfır çağrı korunur; saga yok.

**Scale/Scope**: 2 tedarikçi = 2 adapter + 2 heterojen dataset şekli; 1 event kontratı silinir
(`BuyBoxChanged`); PoolProduct domain sadeleşir; Stock'a 1 yeni tüketim bağlantısı.

## Constitution Check

*GATE: v1.9.0'a karşı değerlendirildi — geçti.*

- **İlke I (BC izolasyonu)**: Servisler-arası yalnız fanout event (`CanonicalProductUpserted`/
  `ProductLinked`; `BuyBoxChanged` SİLİNİR). Supplier.Api ham feed = dış kontrat; Procurement adapter'ı
  ACL sınırıdır (yabancı şekil iç modele sızmaz). DB paylaşımı/senkron RPC yok. ✅
- **İlke I (MCP yalnız agent)**: Feed çekimi REST GET (agent değil, yapısal) — sözleşmeli, MCP yok. ✅
- **İlke II (zengin aggregate)**: PoolProduct zengin kalır; merge/publish kararı aggregate metotlarında;
  tek listing private + getter. Buy-box davranışı SİLİNİR (ölü invariant). ✅
- **İlke III (VSA + CQRS, repo yok)**: Adapter'lar Infrastructure/Feeds (ACL, domain değil); handler'lar
  IDocumentSession + `[Transactional]`. Yeni slice yok, mevcut `PullSupplierFeed`/`PublishPoolProduct`
  sadeleşir. ✅
- **İlke IV (Result)**: aggregate metotları `ResultDomain`; adapter parse hatası satır atlar + loglar
  (Result değil, ingestion politikası — FR-006). ✅
- **İlke V (scope)**: dış yüzey yalnız mock feed uçları (anonim, dev) + manuel pull (Gateway emsali).
  Kullanıcıya dönük uç yok; scope eklenmez. ✅
- **İlke VI (Domain-TDD)**: PoolProduct tek-listing merge/delist/publish-kararı test-first; buy-box
  testleri silinir. Adapter/endpoint canlı-doğrulama. tasks.md test task'larını önce koyar. ✅
- **Artefakt ölçekleme**: Kontrat değişimi (event silme + heterojen feed şekli) → **tam kademe**:
  plan/research/data-model/contracts/quickstart. ✅

**Bilinçli tekrar**: her adapter kendi ham DTO'sunu taşır (paylaşılan feed modeli YOK — BC izolasyonu +
"aynı kavram farklı model"). 2-3 kopya için ortak sınıf açılmaz (kullanıcı tarzı: düz kod).

## Project Structure

### Documentation (this feature)

```text
specs/047-heterogeneous-supplier-feed/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 kararları (D1–D6)
├── data-model.md        # Phase 1 — PoolProduct sadeleşmesi + adapter modeli
├── quickstart.md        # Phase 1 — canlı doğrulama rehberi
├── contracts/
│   ├── heterogeneous-feed-api.md   # per-tedarikçi mock feed şekilleri + route'lar
│   └── integration-events.md       # BuyBoxChanged SİLME + CanonicalProductUpserted tek-kanal
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/supplier/Supplier.Api/
├── Domains/Feeds/FeedEndpointExtension.cs      # DEĞİŞ: per-tedarikçi GET route + response modeli; advance/rev SİL
└── Datasets/
    ├── supplier-a.json                          # DEĞİŞ: tek dosya (rev SİL); A-şekli — ELLE
    └── supplier-b.json                          # DEĞİŞ: tek dosya; B-şekli (gtin/title/cost/warehouseQty) — ELLE

src/services/procurement/Procurement.Api/
├── Infrastructure/Feeds/
│   ├── SupplierFeedClient.cs                    # DEĞİŞ/BÖL: adapter dispatch (tek-DTO çekiş kalkar)
│   ├── Adapters/ISupplierFeedAdapter.cs         # YENİ: ACL sözleşmesi (code → normalize satırlar)
│   ├── Adapters/SupplierAFeedAdapter.cs         # YENİ: A ham DTO → SupplierFeedRowDto
│   ├── Adapters/SupplierBFeedAdapter.cs         # YENİ: B ham DTO → SupplierFeedRowDto (gtin→barcode…)
│   └── FeedPullJob.cs                           # (değişmez; Priority sıralı loop kalır, zararsız)
├── Options/SupplierFeedEndpointsOptions.cs      # YENİ: code→relatif path haritası (Options)
└── Domains/PoolProducts/
    ├── PoolProduct.cs                           # DEĞİŞ: tek-listing; EvaluateBuyBox/PublishedBuyBox/hash-diff SİL
    ├── Entities/SupplierListing.cs              # DEĞİŞ: SupplierPriority + ContentHash alanları SİL
    ├── ValueObjects/PoolProductValueObjects.cs  # DEĞİŞ: BuyBoxDecision + ListingChange SİL; PublishDecision sadeleş
    └── Features/Commands/
        ├── PullSupplierFeed.cs                  # DEĞİŞ: adapter dispatch; her satır koşulsuz upsert→rebuild→publish (tek-gate)
        └── PublishPoolProduct.cs               # DEĞİŞ: buy-box değerlendirme + BuyBoxChanged yayını SİL

src/services/catalog/Catalog.Api/CatalogEventHandlers.cs   # DEĞİŞ: Handle(BuyBoxChanged) SİL
src/services/stock/Stock.Api/StockEventHandlers.cs         # DEĞİŞ: Handle(BuyBoxChanged) → Handle(CanonicalProductUpserted)
src/others/Shared/IntegrationEvents.cs                     # DEĞİŞ: BuyBoxChanged record SİL

tests/Procurement.Api.Tests/                     # DEĞİŞ: buy-box testleri SİL; tek-listing testleri EKLE
```

**Structure Decision**: Mevcut BC yapıları korunur; adapter'lar Procurement `Infrastructure/Feeds/
Adapters/` altında (ACL = altyapı sınırı, domain değil). Domain sadeleşmesi PoolProduct içinde.

## Complexity Tracking

> Constitution Check ihlali yok — tablo boş.

Anayasa ihlali yoktur; söküm karmaşıklığı AZALTIR (bir event kontratı + buy-box makinesi kalkar).
Adapter/ACL yeni desendir ama İlke I'in doğrudan uygulamasıdır (dış-sistem sınırı), sapma değil.