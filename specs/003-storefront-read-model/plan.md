# Implementation Plan: Storefront Composite Read Model (Ürün Vitrin Görünümü)

> **As-built uyarısı (2026-07-20, PR #10 + #11):** Bu plan ilk tasarımı yansıtır; uygulama
> sonrası bazı kararlar değişti — tek `StorefrontView` dokümanı (3 ayrı değil), stok adedi
> (`StockQuantity`), Bootstrap kaldırıldı (saf push-only), event'lerde `OccurredAtUtc` yok,
> MCP tool yok. Güncel tasarım için bkz. [spec.md](./spec.md) Amendment + data-model.md +
> research.md. Aşağıdaki 3-doküman/bootstrap detayları TARİHSEL'dir.

**Branch**: `003-storefront-read-model` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-storefront-read-model/spec.md`

## Summary

Catalog, Stock ve (ürün-bazlı modele dönüştürülen) Discount context'lerinden gelen
veriyi **ProductId** bazında birleştiren, materialize edilmiş, herkese açık bir
composite read model. Yeni bir bounded context (`Storefront.Api`, kendi DB/şema)
olarak kurulur; kaynak servisler kendi verisi değiştiğinde event yayınlar
(writer-publishes), Storefront pasif dinleyip 3 ayrı ProductId-anahtarlı Marten
dokümanını (`CatalogInfo`, `StockInfo`, `DiscountInfo`) idempotent upsert eder. Okuma
(`GetProductStorefrontView`), bu 3 dokümanı Storefront'un KENDİ veritabanında lokal
olarak birleştirip tek yanıt döner — hiçbir zaman kaynak servislere senkron çağrı
yapılmaz (bootstrap hariç, bkz. research.md madde 5). Sipariş ve ödeme kapsam dışıdır.

## Technical Context

**Language/Version**: C# / .NET 10, `Nullable` + `ImplicitUsings` açık (repo geneli).

**Primary Dependencies**: Marten 9.5.0 (+ Marten.Newtonsoft) document store; Wolverine
6.4.1 (in-proc bus + RabbitMQ, `.IntegrateWithWolverine()` transactional outbox);
ASP.NET Core Minimal API + Asp.Versioning; Duende IdentityServer (JWT bearer, scope
doğrulama — anonim M2M token kabul edilir); `ModelContextProtocol.AspNetCore` (MCP
server); Scrutor (DI otomatik kayıt).

**Storage**: PostgreSQL (yeni `storefrontDb`), Marten şeması `storefrontManagement`.

**Testing**: xUnit + Shouldly — saf domain/handler birim testleri (host/entegrasyon
harness'ı yok, repo konvansiyonu). Kapsam: her handler'ın idempotent upsert davranışı,
stale-event guard, `GetProductStorefrontView`'in kısmi-satır/eksik-referans davranışı;
`Discount.Api`'nin yeni ürün-bazlı aggregate'inin invariant'ı (en fazla 1 aktif oran).

**Target Platform**: Aspire AppHost üzerinden orkestre edilen Linux container'lar
(mevcut sistemin bir parçası olarak; bağımsız çalıştırılmaz).

**Project Type**: Dağıtık mikroservis sistemine eklenen yeni bir servis (web-service),
+ mevcut `Catalog.Api`/`Stock.Api`/`Discount.Api`'de değişiklik.

**Performance Goals**: SC-002 — kaynak değişikliği sonrası görünüm ≤5sn içinde güncel;
okuma endpoint'i mevcut servislerle aynı Minimal API gecikme profilinde (ek senkron
fan-out olmadığı için N-kaynak sayısından bağımsız, sabit-maliyetli).

**Constraints**: FR-002/003 — okuma anında Catalog/Stock/Discount'a senkron çağrı YOK
(bootstrap hariç); FR-006 — idempotent upsert + stale-event guard; FR-007 — kimlik
doğrulama gerekmeyen erişim (anonim M2M scope ile).

**Scale/Scope**: İlk teslimat yalnızca ürün-vitrin görünümü; sipariş/ödeme ve
kullanıcıya-özel görünümler ertelenmiştir (spec Assumptions).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Bounded Context İzolasyonu** — KISMİ SAPMA (gerekçeli, bkz. Complexity Tracking).
  `Storefront.Api` yeni, ayrı bir context: kendi DB (`storefrontDb`), kendi şema
  (`storefrontManagement`), kendi domain modeli (rich aggregate değil, projeksiyon).
  Steady-state akış tamamen integration event'lerledir. Bootstrap (bir kerelik, açılışta)
  ise MCP yerine Catalog/Stock/Discount'un kendi mevcut `GET .../all` REST uçlarını
  düz `HttpClient` ile çağırır (implementasyon sırasında kullanıcıyla görüşülüp
  MCP-client makinesi yerine tercih edildi — bkz. Complexity Tracking). Hiçbir servisin
  veritabanına/aggregate'ine doğrudan erişimi yok; yalnızca hedef servisin kendi genel
  REST kontratı çağrılıyor.
- **II. Zengin Aggregate** — KISMİ SAPMA (gerekçeli, bkz. Complexity Tracking):
  Storefront'un 3 dokümanı aggregate değil, düz materialized view'dir — invariant
  taşımaz, otoriter veri üretmez (spec: "Zengin aggregate değildir"). `Discount.Api`
  tarafında ise PASS: `Discount` aggregate'i kalır (`AggregateRoot`'tan türer), sadece
  alanları (UserId→ProductId) ve invariant'ı (bir üründe ≤1 aktif oran) değişir.
- **III. Vertical Slice + CQRS, Repository Yok** — PASS. `Storefront.Api`:
  `Domains/StorefrontView/Features/Queries/GetProductStorefrontView.cs` (tek query
  slice) + event handler'ları (command değil, ayrı bir "Ingestion" alt-klasöründe).
  Repository yok; handler'lar `IDocumentSession`/`IQuerySession` doğrudan kullanır.
- **IV. Result Pattern** — PASS. `GetProductStorefrontView`
  `FeatureObjectResultModel<ProductStorefrontViewResponse>` döner; `Discount.Api`'nin
  yeni `SetProductDiscount`/`RemoveProductDiscount` komutları `FeatureResultModel`/
  `FeatureObjectResultModel<T>` döner, `new` ile değil statik fabrikayla.
- **V. Scope-Tabanlı Yetkilendirme** — PASS. Yeni `AuthorizationScopes.StorefrontRead`;
  rol yok; "herkese açık" olması sıfır-yetkilendirme değil, mevcut anonim-M2M-scope
  deseniyle (research.md madde 6) tutarlı şekilde ele alınıyor.

**Sonuç**: Madde I ve II'de iki, gerekçeli ve sınırlı sapma var (bootstrap'ın düz HTTP
kullanması; Storefront'un kendi projeksiyonları) — aşağıdaki Complexity Tracking'de
belgelendi. Diğer tüm maddeler tam uyumlu. Phase 1 sonrası re-check: sapma aynı kalır
(tasarım değişmedi), yeni bir ihlal oluşmadı. Madde I'deki sapma implementasyon
(`/speckit-implement`) sırasında eklendi — research.md madde 5'in orijinal MCP kararı
pratikte gereksiz karmaşıklık (repo'da ilk kez bir backend servisin MCP client olması)
yarattığı için kullanıcıyla görüşülüp düz HTTP'ye çevrildi.

## Project Structure

### Documentation (this feature)

```text
specs/003-storefront-read-model/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md         # Phase 1 çıktısı
├── quickstart.md         # Phase 1 çıktısı
├── contracts/
│   ├── integration-events.md
│   └── storefront-api.md
└── tasks.md              # Phase 2 çıktısı (/speckit-tasks — henüz üretilmedi)
```

### Source Code (repository root)

**Structure Decision**: Mevcut mikroservis mimarisine, tek bir yeni servis olarak
eklenir; fiziksel klasörler solution klasörleriyle örtüşür (repo konvansiyonu).

```text
src/services/storefront/Storefront.Api/
├── Program.cs                          # Marten+Wolverine+RabbitMQ kablolaması (mevcut servis şablonu)
├── GlobalUsings.cs
├── Dependencies/DependencyExtensions.cs
├── Domains/StorefrontView/
│   ├── CatalogInfo.cs                  # doküman (data-model.md)
│   ├── StockInfo.cs                    # doküman
│   ├── DiscountInfo.cs                 # doküman
│   ├── StorefrontViewEndpointExtension.cs
│   ├── StorefrontMcpTools.cs
│   ├── Features/Queries/GetProductStorefrontView.cs
│   └── Ingestion/                      # event handler'ları (command değil)
│       ├── ProductChangedHandler.cs
│       ├── StockChangedHandler.cs
│       └── DiscountChangedHandler.cs
├── Bootstrap/StorefrontBootstrapHostedService.cs   # research.md madde 5
└── Storefront.Api.csproj

tests/Storefront.Api.Tests/                          # xUnit + Shouldly (repo konvansiyonu)

# Değişen mevcut servisler
src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/
├── CreateProduct.cs      # + ProductChangedEvent publish
├── UpdateProduct.cs      # + IMessageBus + ProductChangedEvent publish
└── DeleteProduct.cs      # + IMessageBus + ProductChangedEvent publish

src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/
├── IncreaseStock.cs      # + IMessageBus + StockChangedEvent publish
└── DecreaseStock.cs      # + IMessageBus + StockChangedEvent publish
src/services/stock/Stock.Api/StockEventHandlers.cs
└── ProductCreatedHandler # + StockChangedEvent publish (ilk stok kaydı açılınca)

src/services/discount/Discount.Api/Domains/Discounts/
├── Discount.cs                          # UserId+DiscountCode -> ProductId (breaking, research.md madde 7)
├── ValueObjects/DiscountCode.cs         # KALDIRILIR
├── Features/Commands/CreateDiscount.cs  # KALDIRILIR
├── Features/Commands/SetProductDiscount.cs      # YENİ
├── Features/Commands/RemoveProductDiscount.cs    # YENİ
├── Features/Queries/GetDiscountByCode.cs         # KALDIRILIR
└── Features/Queries/GetDiscountByProductId.cs    # YENİ
src/services/discount/Discount.Api/EventHandlers.cs
└── OrderCreatedHandler                   # KALDIRILIR (research.md madde 7)

src/others/Shared/IntegrationEvents.cs   # + ProductChangedEvent, StockChangedEvent, DiscountChangedEvent
src/others/Shared/RabbitMqConstants.cs   # + ProductChanged, StockChanged, DiscountChanged
src/others/Shared/Utils/Constants/SchemaConstants.cs      # + StorefrontSchemaName
src/others/Common/Utils/Constants/AuthorizationScopes.cs  # + StorefrontRead

src/aspire/AppHost/AppHost.cs            # + storefrontDb, storefrontApi (+ gateway/web referansları)
src/ui/WebApp/Authentication/TokenService.cs  # ReadScopes += "storefront.read"
```

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Storefront'un 3 dokümanı (`CatalogInfo`/`StockInfo`/`DiscountInfo`) madde II'nin "her serviste tek bir rich aggregate root" kuralına uymuyor — invariant taşımayan düz projeksiyonlar | Bu bir CQRS materialized read-model'dir; invariant'lar zaten kaynak context'lerde (Catalog/Stock/Discount) korunuyor. Storefront hiçbir zaman otoriter veri üretmez (spec FR-005) — bir "aggregate" gibi davranıp iş kuralı taşıması, downstream/conformist rolüyle çelişirdi | Projeksiyonları yapay bir aggregate'e sarmak (ör. sahte bir `ProductStorefrontView` aggregate root'u) hiçbir gerçek invariant korumaz, sadece anayasa maddesini şekilsel olarak "sağlamış" görünür — gerçek bir fayda sağlamadan kod karmaşıklığı ekler |
| Bootstrap (`StorefrontBootstrapHostedService`), madde I'in "yalnızca event/MCP" kuralına uymuyor — Catalog/Stock/Discount'un kendi REST uçlarını (`GET .../all`) düz `HttpClient` ile çağırıyor | Bootstrap tek seferlik, açılışta çalışan bir arka plan işidir (FR-011); MCP-client'ı elle sürmek (JSON-RPC tool-call framing, gevşek-tipli sonuç ayrıştırma) repo'da ilk kez bir backend servisi (agent değil) MCP client'a çevirirdi — kırılgan ve fazladan makine | Orijinal MCP kararı (research.md madde 5) korunabilirdi ama pratikte tipli DTO'ya deserialize eden düz bir `GetFromJsonAsync` çağrısından çok daha karmaşık; hedef uç zaten her iki durumda da servisin KENDİ genel kontratı (DB'sine değil) — izolasyon ilkesi ihlal edilmiyor, sadece taşıma protokolü değişiyor |