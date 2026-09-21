# Implementation Plan: Kampanya İndirim Motoru (Discount.Api)

**Branch**: `079-discount-campaigns` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/079-discount-campaigns/spec.md`

## Summary

Yeni **Discount.Api** BC'si admin-güdümlü kampanya indirimini yönetir. Admin metinle süzgeç (kategori/
yazar/yayınevi/tek-kitap) + yüzde + tarih verir; sistem süzgeci uygulama anında kitap setine çözer (kendi
`ProductCatalogRef` read-model'inden), her kitaba `ProductDiscount` işler — **kitap başına tek indirim**
(varsa atla, PK teklik). Discount.Api **fiyat tutmaz** — saf yüzde otoritesi. Kitap başına indirim
`ProductDiscountChanged` event'iyle Storefront'a itilir; Storefront etkin fiyatı kendi liste fiyatından
hesaplar, `query_storefront` inline döndürür. Süre yönetimi **per-kampanya Wolverine scheduled message**
(start aktifle, end o kampanyanın kitaplarını temizle; guard'lı idempotent; view-guard yedek). Checkout'ta
Order.Api sağası **gRPC** ile aktif yüzdeyi canlı doğrular. BestWins/overlap YOK (kitap başına tek indirim).
Kupon/sabit-tutar/mail v1 dışı. 080'den bağımsız.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5 (Postgres doc store, Newtonsoft), WolverineFx 6.4 (in-proc bus +
RabbitMQ fanout + `ScheduleAsync` scheduled message), Grpc.AspNetCore 2.67 (checkout S2S), MCP.AspNetCore
1.4 (admin MCP), Scrutor 7 (DI). LLM YOK. Şablon: `src/services/payment/Payment.Api`.

**Storage**: Yeni `discountDb` (Postgres + Marten; `ApplyAllDatabaseChangesOnStartup`).

**Testing**: xUnit + Shouldly; Domain-TDD (Campaign aggregate test-first, İLKE VI).

**Target Platform**: Aspire AppHost içinde container servis.

**Project Type**: Web-service BC — REST yok; yüzey MCP-admin + checkout gRPC + event.

**Performance Goals**: Kampanya açılışında kapsam vitrinde &lt;5 sn (SC-001); checkout gRPC p95 &lt;100ms.

**Constraints**: BC izolasyonu (kendi DB, fiyat tutmaz); admin MCP-only; scheduling kampanya sayısıyla
ölçeklenir (ürünle değil — 1 kampanya = ≤2 mesaj, kapsamı kaç ürün olursa olsun).

**Scale/Scope**: Katalog ~19k ürün; kampanya sayısı onlarca (≤~100 scheduled envelope). En geniş fire =
geniş süzgeçli (ör. büyük kategori/yayınevi) kampanyanın kapsamı kadar `ProductDiscount` apply/temizle
(bir defa, fire anında; kitap-başı schedule DEĞİL).

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

- **İLKE I (BC İzolasyonu)** ✓ — kendi `discountDb`; başka BC tablosuna erişmez. Ürün/kategori bilgisi
  `ProductChangedEvent`'ten beslenen kendi `ProductCatalogRef` kopyasında (event = sanksiyonlu kanal).
  Discount.Api **fiyat TUTMAZ** — yalnız `{productId, categoryId, published}`.
- **İLKE II (Zengin Aggregate)** ✓ — `Campaign : AggregateRoot`; yüzde 1-99, bitiş>başlangıç invariant'ları
  aggregate içinde; enum ScopeType (Category/Author/Publisher/Product) + CampaignStatus `Campaign.cs`'te.
  `ProductDiscount`/`ProductCatalogRef` aggregate değil (materyalize/izdüşüm read-model, ayrı yerleşir).
- **İLKE III (VSA + CQRS)** ✓ — `Features/Agents/Commands` (create/edit/cancel) + `Queries` (list); handler
  doğrudan `IDocumentSession`; Repository yok. Scheduled fire handler'ı `Process/` (kullanıcı değil süreç).
- **İLKE IV (Result)** ✓ — aggregate `ResultDomain`, handler `FeatureObjectResultModel<T>`.
- **İLKE V (Scope Yetki)** ✓ — yeni scope: `discount.read` (S2S checkout), `AdminDiscountWrite` (admin MCP).
  `KnownScopes` + rol→scope map'e eklenir; allowlist `Shared/McpToolNames.cs DiscountAdminTools` + Program.cs
  `ConfigureSessionOptions`.
- **İLKE VI (Domain-TDD)** ✓ — Campaign davranışı (Create/Cancel + invariant) test-first; süzgeç-çözüm/
  apply-skip/handler/gRPC/consumer test-sonra.
- **İLKE VII (FLOW.md)** ✓ — `src/services/discount/FLOW.md` + `.csproj` linked-file; check-flow-links guard.

**tr-TR Marten alias tuzağı:** `Campaign` / `ProductCatalogRef` doc adlarında dotless-ı YOK → risk yok;
yine de savunma amaçlı `.DocumentAlias("campaign")` verilir (077 dersi).

**Wolverine keşif tuzağı:** scheduled-fire handler + `CatalogConsumers` `*Handler`/`*Consumer` ile bitmeli
YA DA `opts.Discovery.IncludeType(...)` Program.cs'te zorunlu (unutulursa mesaj sessizce yutulur).

**Gate: PASS** — ihlal yok; Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/079-discount-campaigns/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/            # discount_query.proto + integration-events.md + mcp-admin-tools.md
└── tasks.md              # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/discount/
├── FLOW.md
└── Discount.Api/
    ├── Discount.Api.csproj              # sürümsüz PackageReference; <None Include="..\FLOW.md" Link=>
    ├── Program.cs                       # Marten(discountDb)+Wolverine(RabbitMQ+ScheduleAsync)+gRPC+MCP(/mcp-admin)
    ├── GlobalUsings.cs · Properties/launchSettings.json
    ├── Options/DiscountOptions.cs · Constants/DiscountResourceConstants.cs
    ├── Dependencies/DependencyExtensions.cs
    ├── Domains/Campaigns/
    │   ├── Campaign.cs                   # aggregate + ScopeType/CampaignStatus enum
    │   ├── CampaignSelectionResolver.cs  # süzgeç → kitap seti (ProductCatalogRef sorgusu; tek-kitap doğrudan)
    │   └── Features/Agents/
    │       ├── Commands/CreateCampaign.cs   # + [McpServerToolType] admin_create_campaign (çöz+apply-skip+schedule)
    │       ├── Commands/CancelCampaign.cs   # + admin_cancel_campaign (kitapları temizle)
    │       └── Queries/ListCampaigns.cs     # + admin_list_campaigns
    ├── Domains/ProductDiscount/ProductDiscount.cs        # materyalize kitap-başı indirim (PK=productId)
    ├── Domains/ProductCatalogRef/ProductCatalogRef.cs    # destek read-model (süzgeç çözümü)
    ├── CatalogConsumers.cs               # ProductChangedEvent → ProductCatalogRef upsert
    ├── Process/CampaignScheduleHandler.cs # CampaignActivated/CampaignEnded fire → apply/temizle+push (guard'lı)
    └── Grpc/DiscountQueryGrpcService.cs   # checkout S2S: GetProductDiscounts

src/others/Shared/
├── IntegrationEvents.cs                  # + ProductDiscountChanged record
├── McpToolNames.cs                       # + DiscountAdminTools
├── RabbitMqConstants.cs                  # + discount exchange/queue sabitleri
└── Protos/discount_query.proto           # checkout gRPC kontratı

# Dokunulan mevcut servisler
src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/StorefrontView.cs   # + DiscountPct/StartsAt/EndsAt + ApplyDiscount
├── DiscountConsumers.cs                        # + ProductDiscountChanged → ApplyDiscount
├── AgentSql/StorefrontSellableSchema.cs        # + discount kolonları + view-guard CASE (effective_price)
├── Domains/.../Queries/QueryStorefront.cs      # SchemaBlock'a yeni kolonlar
└── Program.cs                                  # DiscountConsumers IncludeType + queue binding
src/services/order/Order.Api/
├── Grpc/SagaTokenHandler.cs                    # scope'a discount.read
├── Grpc/DiscountClient.cs                       # yeni gRPC client proxy
├── Domains/Orders/Features/Agents/Commands/StartPayment.cs  # discount uygula (tutar hesabı)
└── Program.cs                                   # AddGrpcClient<DiscountQueryClient>+HttpMessageHandler
src/aspire/AppHost/AppHost.cs                    # discountDb + AddProject(discount-api)+WithReference+WaitFor
KnownScopes + rol seed                           # discount.read + AdminDiscountWrite
```

**Structure Decision**: Payment.Api şablonu. Süreç-güdümlü handler'lar `Domains/` dışında
(`CatalogConsumers` = kaynak Catalog; `Process/CampaignScheduleHandler` = BC'nin kendi dayanıklı süreci);
kullanıcı/admin slice'ları `Features/Agents/`. Wolverine `Process`/`Consumers` sınıfları `IncludeType`
ile kayıtlı olmalı. gRPC server ince sarmalayıcı (mantık doğrudan servis class'ında, tek çağıran Order).

## Complexity Tracking

Anayasa ihlali yok — boş.