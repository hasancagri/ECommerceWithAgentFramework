# Implementation Plan: MCP-Only Yüzey — Domain REST Söküm + Catalog Admin Parite

**Branch**: `074-mcp-only-rest-teardown` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/074-mcp-only-rest-teardown/spec.md`

## Summary

Mağazanın iş yüzeyini tam MCP-only'e taşı: catalog/stock/customer-merchant/checkout domain REST
endpoint'lerini + `.http` + yetim gateway proxy'sini sök; kullanıcı/admin yüzeyli her Command/Query
ya (a) Agent ikizi VARSA REST'iyle silinir ya (b) yoksa önce `Features/Agents/*ForAgent` slice'ı +
`/mcp-admin` tool'u kurulur (070 deseni). Kritik/iç slice'lar (S2S/saga/import) dokunulmaz. Son durum:
`Features/Agents` = tek müşteri+admin iş yüzeyi; `Features/Commands|Queries` yalnız iç slice tutar.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres doc/event store), Wolverine (in-proc bus + RabbitMQ),
ModelContextProtocol SDK (`[McpServerTool]`, `MapMcp`, `ConfigureSessionOptions`), OpenIddict (auth),
YARP (gateway). Yeni bağımlılık YOK.

**Storage**: Catalog `catalogDb` (Product/Category/Author/Publisher/ProductTag/SpecificationAttribute
+ append-only `ProductPriceChange` + `AdminActionLog`); diğer BC'ler kendi DB'leri. Yeni tablo YOK
(yeni tool'lar var olan aggregate'leri + AdminActionLog'u kullanır).

**Testing**: xUnit + Shouldly. Domain-TDD (İLKE VI) yalnız saf domain için; yeni tool'lar var olan
aggregate metotlarını (Create/Rename/SetDimensions/SetSeo/AddTag/AddOption) çağırdığından yeni domain
davranışı ≈ yok → ağırlık handler/tool wiring (test-sonra + canlı doğrulama).

**Target Platform**: Linux container (Aspire AppHost orchestration).

**Project Type**: Mikroservis (DDD/VSA) — çok-servisli backend, agent-only yüzey.

**Performance Goals**: Yüzey değişimi; performans hedefi yok (davranış-koruyan söküm + ince sarmalayıcı).

**Constraints**: Söküm hiçbir canlı yolu kırmamalı (auth/MCP-infra/S2S/gRPC korunur); anonim `/mcp`
keşif seti değişmemeli; her admin yazma scope + `AdminActionLog` izli.

**Scale/Scope**: 4 servis (catalog/stock/customer/checkout) + gateway + 5 `.http`. ~14 REST endpoint
söküm, ~9 mevcut Command/Query REST-only silme, ~15 yeni catalog Agent slice + tool.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İLKE I (BC izolasyonu):** ✅ Söküm izolasyonu güçlendirir (yetim çapraz-yüzey azalır). S2S/gRPC/saga
  kontratları (payment-context, merchant-key, basket gRPC, checkout broker) DOKUNULMAZ.
- **İLKE II (zengin aggregate):** ✅ Yeni tool yeni aggregate açmaz; var olan davranış metotlarını çağırır.
  Yeni domain kuralı yok → yeni invariant yok.
- **İLKE III (VSA+CQRS, MCP ince sarmalayıcı):** ✅ Merkez ilke. Yeni tool'lar `Features/Agents/*ForAgent`
  slice'ı çağıran ince sarmalayıcı; Agent slice İZOLE (kendi handler'ı, Commands/Queries reuse YOK).
  ⚠️ Sapma NOTU: mevcut admin Agent slice'ları (070) MCP tool'undan `IMessageBus.InvokeAsync` ile
  çağrılıyor (aynı BC-içi) — İLKE III'ün "MCP tool aynı command'ı IMessageBus ile çağırır" kuralına uygun.
- **İLKE IV (Result):** ✅ Yeni handler'lar `FeatureObjectResultModel<T>`/`FeatureListResultModel<T>`
  döner; hata `MessageItem.Code` = `CatalogResourceConstants` sabiti.
- **İLKE V (scope yetki):** ✅ Yeni yazma tool'ları `[RequiredScope(CatalogWrite)]`; okuma admin tool'ları
  `/mcp-admin` (login) altında. Yeni scope GEREKMEZ (mevcut `CatalogWrite` yeter — Assumptions'ta doğrulanır).
- **İLKE VI (Domain-TDD):** ✅ Yeni saf-domain davranışı yok (metotlar mevcut). Test task'ı yeni domain
  için gerekmez; wiring test-sonra.
- **İLKE VII (FLOW.md):** ⚠️ Catalog domain SÜRECİ değişmez (aynı davranışlar, yüzey değişir) → FLOW.md
  tetiklenmeyebilir. `create_product` YENİ giriş adımı ekliyor (doktrin kayması) → catalog FLOW.md'de
  "ürün girişi" satırı güncellenir (aynı PR). Order FLOW.md refactor'da (ayrı PR) değişmedi.

**Gate sonucu: PASS** (sapma yok; İLKE III/VII notları tasarımda karşılandı).

## Project Structure

### Documentation (this feature)

```text
specs/074-mcp-only-rest-teardown/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — çevir/sök/koru kararları + create_product ISBN çakışma
├── data-model.md        # Phase 1 — dokunulan aggregate + AdminActionLog (yeni tablo yok)
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları (chat E2E + admin /mcp-admin)
├── contracts/           # Phase 1 — yeni MCP tool sözleşmeleri (ad + param + response)
│   └── catalog-admin-tools.md
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Domains/
│   ├── Products/
│   │   ├── Features/Agents/          # YENİ: AdminCreateProductForAgent, AdminSetDimensionsForAgent,
│   │   │                             #       AdminSetSeoForAgent, AdminAssignTagForAgent,
│   │   │                             #       AdminRemoveTagForAgent (var olan Command mantığı taşınır)
│   │   ├── Features/Commands/         # SÖK: CreateProduct, SetProductDimensions, SetProductSeo,
│   │   │                             #      AssignTagToProduct, RemoveTagFromProduct, UpdateProduct,
│   │   │                             #      SetProductPublished (REST; Agent ikizi var/kurulur)
│   │   │                             # KAL: ImportBook (051 import — kritik)
│   │   ├── Features/Queries/          # SÖK: GetProductById, AdminGetProduct, AdminListProducts,
│   │   │                             #      GetProductPriceHistory (Agent ikizi var)
│   │   ├── ProductMcpTools.cs         # YENİ admin tool sarmalayıcıları eklenir
│   │   └── ProductEndpointExtension.cs# SÖK (map kalmayınca)
│   ├── Categories/  (Create/Update → Agent; GetCategories REST sök)
│   ├── Authors/     (Create → Agent; GetAuthors REST sök)
│   ├── Publishers/  (GetPublishers REST sök)
│   ├── ProductTags/ (Create/Rename → Agent + list Agent; REST sök)
│   └── SpecificationAttributes/ (Create/AddOption + list → Agent; REST sök)
├── AdminAudit/AdminActionLog.cs       # DEĞİŞMEZ (yeni tool'lar Executed/Rejected yazar)
└── Program.cs                          # ConfigureSessionOptions filtresi DEĞİŞMEZ (yeni admin tool'lar
                                        # attribute'la otomatik /mcp-admin'de)

src/services/stock/Stock.Api/           # SÖK: 5 REST endpoint + SetStockQuantity/Increase/Decrease/
                                        #      GetAllStock/GetStockByProductId REST slice (Agent ikizi var;
                                        #      GetAllStock için Agent kur veya at). KAL: CommitStock/RevertCommitStock (saga)
src/services/customer/Customer.Api/     # SÖK: merchant-information admin REST (get/set) + slice.
                                        # KAL: /internal/payment-context + /internal/merchant-key (S2S) DOKUNMA
src/services/checkout/Checkout.Orchestrator/ # SÖK: POST /checkout REST + CheckoutEndpointExtension
src/services/gateway/Gateway/           # SÖK: appsettings.Development.json catalog-route + ClientCredential
src/others/Shared/McpToolNames.cs       # YENİ: CatalogAdminTools'a create/category/author/tag/spec/dim/seo adları
src/services/*/                         # SÖK: 5 .http dosyası
```

**Structure Decision**: Mevcut VSA yapısı korunur. "Çevirme" = mevcut `Features/Commands` handler
mantığını `Features/Agents/*ForAgent` slice'ına taşı (UserId + `[RequiredScope]` + `AdminActionLog` +
kısmi-alan deseni ekleyerek — `AdminUpdateProductForAgent` emsali), MCP tool sarmalayıcısını
`*McpTools.cs`'e ekle, eski Command/Query + REST endpoint'i sök. Yeni proje/klasör AÇILMAZ.

## Complexity Tracking

> Constitution Check PASS — sapma yok. Bu tablo boş.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |