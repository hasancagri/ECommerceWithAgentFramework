# Implementation Plan: Tedarikçi Feed'i = Stoğun Tek Otoritesi

**Branch**: `014-supplier-stock-authority` | **Date**: 2026-07-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014-supplier-stock-authority/spec.md`

## Summary

Stok adedi yalnız ingestion akışına eklenen **StockWrite executor**'dan yazılır; feed
tek otoritedir ve OnHand'i mutlak değere eşitler (create + update). Bunun için: (1)
IngestionAgent workflow'una Catalog→**Stock**→Discount adımı eklenir; (2) Catalog'un
stok taşıması sökülür — `ProductCreatedEvent` (yalnız stok taşıyan ölü kontrat) komple
kaldırılır, `initialStock` argümanı silinir; (3) Stock'taki ProductCreated→seed handler'ı
ve manuel `set_stock` REST ucu kaldırılır. `set_stock` MCP tool'u + `SetStock` command
KORUNUR (StockWrite onu çağırır). 012 Model C'nin "feed stoğu ezmez" duruşu tersine döner.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (document store), Wolverine (bus + RabbitMQ), Microsoft
Agent Framework (MAF Workflows — Executor/WorkflowBuilder), MCP (ModelContextProtocol client)

**Storage**: Servis-başına Postgres (Marten). **Şema değişikliği YOK** — `ProductStock`
aggregate'i ve `stockDb` şeması aynı kalır; yalnız yazım topolojisi değişir.

**Testing**: xUnit + Shouldly (saf domain birim testleri). Entegrasyon davranışı repo
konvansiyonuyla **canlı/manuel** doğrulanır (007/012/013 emsali); yeni entegrasyon harness'ı yok.

**Target Platform**: Aspire AppHost ile orkestre edilen dağıtık sistem (Postgres + RabbitMQ).

**Project Type**: Mikroservisler (Catalog, Stock, IngestionAgent) + paylaşılan kontratlar.

**Performance/Constraints/Scale**: Düşük hacim (feed ingestion, mesaj başına workflow);
özel performans hedefi yok. Kısıt: oversell yasağı (checkout `AvailableAt`/`IsOversoldAt`
ile korunur — Stock aggregate'i zaten sağlıyor).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **I. Bounded Context İzolasyonu**: ✅ **Güçlenir.** Catalog artık Stock'un derdini
  (stok adedi) taşımaz → `ProductCreatedEvent` kalkar. Stok yazımı sanksiyonlu MCP
  kanalıyla (Stock'un `set_stock` tool'u) yapılır; doğrudan DB erişimi yok.
- **II. Zengin Aggregate**: ✅ Mevcut `ProductStock.SetQuantity` (mutlak, negatif-yasak
  invariant'ı) kullanılır; yeni aggregate/anemik mantık yok.
- **III. Vertical Slice + CQRS, Repository Yok**: ✅ StockWrite, Stock'un `Features/Agent`
  slice'ını saran `set_stock` MCP tool'unu çağırır (ince sarmalayıcı, iş mantığı Stock'ta).
  Repository yok; IngestionAgent MCP client.
- **IV. Result Pattern**: ✅ `SetStock` `FeatureObjectResultModel` döner; değişmez.
- **V. Scope-Tabanlı Yetki (Rol Yok)**: ✅ Stok yazım yolu 005'ten beri anonim (kullanıcı
  kararı); manuel REST ucu kalkınca yüzey daralır. Rol getirilmez.

**Model C tersine çevrimi** anayasa ihlali DEĞİLDİR — Model C 012 spec'inin kararıdır,
anayasada yer almaz. Yalnız 012 spec notunun güncellenmesi gerekir (dokümantasyon
uzlaştırması; tasks'ta görev). **İhlal yok → Complexity Tracking boş.**

## Project Structure

### Documentation (this feature)

```text
specs/014-supplier-stock-authority/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — kararlar
├── data-model.md        # Phase 1 — (şema yok) kontrat/varlık deltası
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları
├── contracts/           # Phase 1 — kontrat deltaları (event + MCP + REST)
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/agents/IngestionAgent/Workflows/
├── SupplierSnapshotHandler.cs          # DEĞİŞ: Catalog→Stock→Discount edge + StockWriterAgent inject
├── 01_CatalogWrite/CatalogWriterAgent.cs  # DEĞİŞ: ["initialStock"] argümanı silinir
└── 03_StockWrite/                       # YENİ klasör
    ├── StockWriteExecutor.cs            # YENİ: set_stock çağıran ara executor (short-circuit'li)
    └── StockWriterAgent.cs              # YENİ: stock MCP'sine set_stock sarmalayıcı
src/agents/IngestionAgent/Program.cs     # DEĞİŞ: stockMcp + AddSingleton<StockWriterAgent>

src/services/catalog/Catalog.Api/
├── Domains/Products/Features/Commands/CreateProduct.cs  # DEĞİŞ: InitialStock + ProductCreatedEvent publish silinir
├── Domains/Products/Features/Agent/UpsertProduct.cs     # DEĞİŞ: InitialStock alanı + geçişi silinir
├── Domains/Products/ProductMcpTools.cs                  # DEĞİŞ: upsert_product'tan initialStock parametresi silinir
└── Program.cs                                           # DEĞİŞ: ProductCreated exchange publish deklarasyonu silinir

src/services/stock/Stock.Api/
├── StockEventHandlers.cs                # SİL: ProductCreatedHandler (dosya boşalırsa silinir)
├── Domains/Stocks/Features/Commands/SetStock.cs  # DEĞİŞ: SetStockCommandEndpoint (REST /set) silinir; command+handler kalır
├── Domains/Stocks/StockEndpointExtension.cs (varsa) # DEĞİŞ: /set map çağrısı silinir
└── Program.cs                           # DEĞİŞ: ProductCreated exchange declare/bind/listen silinir

src/others/Shared/
├── IntegrationEvents.cs                 # DEĞİŞ: ProductCreatedEvent record silinir
├── Payloads/ProductStockInfo.cs         # SİL: yalnız ProductCreatedEvent kullanıyordu
└── RabbitMqConstants.cs                 # DEĞİŞ: ProductCreated sabit sınıfı silinir
```

**Structure Decision**: Mevcut mikroservis + vertical-slice yapısı korunur. Yeni kod tek
yer: IngestionAgent `Workflows/03_StockWrite/` (007'de silinen klasörün geri gelmesi,
feed-otoriter semantikle). Gerisi silme/sadeleştirme.

## Complexity Tracking

> Constitution Check ihlali yok — bu bölüm boş.