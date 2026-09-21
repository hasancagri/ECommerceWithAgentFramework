---
description: "Task list — Kampanya İndirim Motoru (Discount.Api)"
---

# Tasks: Kampanya İndirim Motoru (Discount.Api)

**Input**: `specs/079-discount-campaigns/` (plan, spec, research, data-model, contracts, quickstart)

**Tests**: İLKE VI (Domain-TDD) → Campaign aggregate davranışı + saf resolver/apply mantığı **test-first**
(önce başarısız test task'ı). Handler/gRPC/consumer/endpoint = test-sonra (canlı doğrulama, quickstart).

**Organization**: Kullanıcı hikayesi bazlı. Şablon: `src/services/payment/Payment.Api`.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup (Discount.Api iskeleti)

- [X] T001 Discount.Api proje iskeleti (`src/services/discount/Discount.Api/`): `.csproj` (sürümsüz
  PackageReference — Marten/Wolverine/gRPC/MCP/Scrutor + `<None Include="..\FLOW.md" Link="FLOW.md"/>`),
  `GlobalUsings.cs`, `Properties/launchSettings.json` (dev port 5045/7131), `Dependencies/DependencyExtensions.cs`,
  `Constants/DiscountResourceConstants.cs`, `Options/DiscountOptions.cs`
- [X] T002 `Program.cs` iskeleti: Marten(`discountDb` + `.DocumentAlias` + `ApplyAllDatabaseChangesOnStartup`)
  + Wolverine(`UseWolverine` + RabbitMQ + ScheduleAsync) + `AddAllDependencies()` + auth/scope middleware
- [X] T003 [P] AppHost kaydı: `postgres.AddDatabase("discountDb")` + `AddProject<Projects.Discount_Api>
  ("discount-api")` + `WithReference(discountDb/rabbit)` + `WaitFor` (`src/aspire/AppHost/AppHost.cs`)
- [X] T004 [P] `src/services/discount/FLOW.md` — domain süreç (kampanya aç→süz→uygula-atla→push→zamanla→bitişte temizle)
- [X] T005 [P] Yeni scope'lar `discount.read` + `AdminDiscountWrite` → `KnownScopes` registry + rol→scope seed

---

## Phase 2: Foundational (tüm hikayeler önce)

**⚠️ Bu faz bitmeden hikaye işi başlamaz.**

- [X] T006 [P] Shared: `ProductDiscountChanged(ProductId, DiscountPct, StartsAt?, EndsAt?)` record
  (`src/others/Shared/IntegrationEvents.cs`)
- [X] T007 [P] Shared: `DiscountAdminTools` sabitleri (create/cancel/list) (`src/others/Shared/McpToolNames.cs`)
- [X] T008 [P] Shared: discount exchange/queue sabitleri (`src/others/Shared/RabbitMqConstants.cs`)
- [X] T009 [P] Shared: `discount_query.proto` (`GetProductDiscounts`) (`src/others/Shared/Protos/discount_query.proto`)
- [X] T010 [P] `ProductCatalogRef` read-model `{productId, categoryId, authorIds, publisherId, published}`
  (`Discount.Api/Domains/ProductCatalogRef/ProductCatalogRef.cs`)
- [X] T011 `CatalogConsumers`: `ProductChangedEvent`→`ProductCatalogRef` upsert + `Program.cs`
  `opts.Discovery.IncludeType` + queue binding (`Discount.Api/CatalogConsumers.cs`)

---

## Phase 3: US1 — Admin süzgeçle indirim açar, listede inline (Priority: P1) 🎯 MVP

**Bağımsız test**: kategori kampanyası aç → `query_storefront` o kategoriyi listele → indirimli fiyat +
`discount_ends_at` inline; atlanan/kampanyasız kitap liste fiyatı.

- [X] T012 [P] [US1] TEST Campaign aggregate (`tests/Discount.Api.Tests/CampaignTests.cs`): Create invariant
  (%0/100/150 red, endsAt<startsAt red, boş name red, eksik scopeRef red, scopeType↔scopeRef tutarsızlığı red;
  0-kitaba-çözülen geçerli süzgeç red DEĞİL — resolver testi T014); Cancel→IsEffectiveAt false
- [X] T013 [US1] `Campaign` aggregate + `ScopeType{Category,Author,Publisher,Product}` + `CampaignStatus` enum
  (`Discount.Api/Domains/Campaigns/Campaign.cs`)
- [X] T014 [P] [US1] TEST `CampaignSelectionResolver` (`tests/.../SelectionResolverTests.cs`): kategori/yazar/
  yayınevi süzgeci doğru kitap seti; tek-kitap doğrudan; çözülmeyen süzgeç boş set
- [X] T015 [US1] `CampaignSelectionResolver` — süzgeç→kitap seti (`ProductCatalogRef` sorgusu)
  (`Discount.Api/Domains/Campaigns/CampaignSelectionResolver.cs`)
- [X] T016 [P] [US1] TEST apply-skip (`tests/.../ProductDiscountApplyTests.cs`): zaten indirimli kitap atlanır
  (insert-if-not-exists), yoksa eklenir
- [X] T017 [US1] `ProductDiscount` read-model `{productId(PK), campaignId, percentage, startsAt, endsAt}`
  (`Discount.Api/Domains/ProductDiscount/ProductDiscount.cs`)
- [X] T018 [US1] `CreateCampaign` command + `[McpServerToolType] admin_create_campaign`: Campaign.Create→
  **startsAt≤now ise aktifleştir** (resolver→her kitaba ProductDiscount yoksa Store→`ProductDiscountChanged`
  yayınla; `{applied, skipped}` döner), **gelecek tarihli ise ProductDiscount YAZMA** (Scheduled, slot tutmaz;
  `{scheduled}` döner)→ScheduleAsync(start,end) (`Discount.Api/Domains/Campaigns/Features/Agents/Commands/CreateCampaign.cs`)
- [X] T019 [US1] `CancelCampaign` command + `admin_cancel_campaign`: campaignId'nin ProductDiscount'larını
  sil + `ProductDiscountChanged(pct:0)` yayınla (`.../Features/Agents/Commands/CancelCampaign.cs`)
- [X] T020 [P] [US1] `ListCampaigns` query + `admin_list_campaigns` (`.../Features/Agents/Queries/ListCampaigns.cs`)
- [X] T021 [US1] Admin allowlist: `discountAdminToolNames` + `ConfigureSessionOptions` (`/mcp-admin` budama) +
  `AddMcpAdminResourceMetadata(...,"discount",...)` + `MapMcp("/mcp-admin")` (`Discount.Api/Program.cs`)
- [X] T022 [US1] Discount.Api `ProductDiscountChanged` exchange deklare + publish yolu (`Discount.Api/Program.cs`)
- [X] T023 [P] [US1] Storefront: `StorefrontView` indirim alanları (`DiscountPct/StartsAt/EndsAt`) + `ApplyDiscount`
  metodu (`Storefront.Api/Domains/StorefrontView/StorefrontView.cs`)
- [X] T024 [US1] Storefront: `DiscountConsumers` `ProductDiscountChanged`→`ApplyDiscount` + `Program.cs`
  `IncludeType` + queue binding (`Storefront.Api/DiscountConsumers.cs`)
- [X] T025 [US1] Storefront: `StorefrontSellableSchema` `discount_pct/starts_at/ends_at` kolonları + `BuildViewDdl`
  `effective_price` CASE (view-guard `now BETWEEN starts_at AND ends_at`) (`Storefront.Api/AgentSql/StorefrontSellableSchema.cs`)
- [X] T026 [US1] Storefront: `QueryStorefront` SchemaBlock'a yeni kolonlar → `scripts/check-agent-query-schema.sh`
  yeşil (`Storefront.Api/Domains/StorefrontView/Features/Agents/Queries/QueryStorefront.cs`)

**Checkpoint**: US1 tek başına çalışır — admin indirim açar, müşteri listede indirimli görür (bitiş/checkout hariç).

---

## Phase 4: US2 — Süre dolunca kitaplar otomatik listeye döner (Priority: P1)

**Bağımsız test**: bitişi yakın kampanya aç → süre dolunca vitrin o kitaplar için liste fiyatı.

- [X] T027 [P] [US2] TEST scheduled handler guard (`tests/.../CampaignScheduleTests.cs`): bayat/iptal mesaj
  no-op; bitiş kampanyanın ProductDiscount'larını temizler
- [X] T028 [US2] `CampaignActivated`/`CampaignEnded` scheduled mesajları — `CreateCampaign`'da `ScheduleAsync(
  startsAt, endsAt)` (`.../Commands/CreateCampaign.cs`)
- [X] T029 [US2] `Process/CampaignScheduleHandler`: **start-fire→aktifleştir** (resolver→her kitaba
  ProductDiscount yoksa Store→`ProductDiscountChanged` yayınla; ilk-AKTİF-kazanır skip); **end-fire→**
  campaignId ProductDiscount sil + `ProductDiscountChanged(pct:0)`; **guard'lı idempotent** (aggregate güncel
  duruma bakar, bayat/iptal mesaj no-op); `Program.cs` `IncludeType` (`Discount.Api/Process/CampaignScheduleHandler.cs`)
- [X] T030 [US2] View-guard doğrula: `now` pencere dışında `effective_price`=liste, discount alanları boş
  döner (fire gecikmesi yedeği) (`Storefront.Api/AgentSql/StorefrontSellableSchema.cs`)

---

## Phase 5: US3 — Checkout canlı doğrulama + sepette grace yok (Priority: P2)

**Bağımsız test**: indirimli kitapla `start_payment` → tutar indirimli; kampanya bitince → tutar liste.

- [X] T031 [US3] Discount.Api gRPC server `DiscountQueryGrpcService.GetProductDiscounts` (aktif pencere-içi
  ProductDiscount yüzdeleri) + `MapGrpcService().RequireAuthorization(discount.read)` (`Discount.Api/Grpc/DiscountQueryGrpcService.cs`)
- [X] T032 [US3] Order.Api: `AddGrpcClient<DiscountQueryClient>().AddHttpMessageHandler<SagaTokenHandler>()`
  + `DiscountClient` proxy + `SagaTokenHandler` scope'una `discount.read` ekle (`Order.Api/Program.cs`,
  `Order.Api/Grpc/SagaTokenHandler.cs`, `Order.Api/Grpc/DiscountClient.cs`)
- [X] T033 [US3] Order.Api `StartPayment`: sepet kalem id'leriyle gRPC çağır → aktif yüzdeyi satır fiyatına
  uygula → tutar (grace yok, aktif değilse liste) (`Order.Api/Domains/Orders/Features/Agents/Commands/StartPayment.cs`)

---

## Phase 6: Polish & Cross-Cutting

- [X] T034 [P] `scripts/check-flow-links.sh` yeşil — `FLOW.md` anchor tip adları kodda var
- [ ] T035 [P] quickstart Senaryo 1-6 canlı doğrulama (Aspire AppHost)
- [X] T036 [P] `dotnet build` + `dotnet test tests/Discount.Api.Tests` + bağımlı test projeleri
  (Storefront/Order — rename/kontrat kırığı yakala) yeşil

---

## Dependencies

- **Setup (P1)** → **Foundational (P2)** → hikayeler.
- **US1 (P3)** = MVP; foundational'a bağlı. T013←T012, T015←T014, T017←T016 (test-first).
- **US2 (P4)** US1'in apply/clear + ProductDiscount'una bağlı (T017/T018).
- **US3 (P5)** US1'in ProductDiscount'una bağlı (T017); Order.Api gRPC bağımsız kurulabilir.
- **Polish (P6)** hepsinden sonra.

## Parallel Opportunities

- Setup: T003/T004/T005 [P] birlikte (farklı dosyalar).
- Foundational: T006-T010 [P] (Shared record/proto/const + read-model ayrı dosyalar).
- US1 test'leri T012/T014/T016 [P]; Storefront T023 [P] Discount.Api tarafından bağımsız başlar.

## Implementation Strategy

- **MVP = US1** (admin indirim açar + listede inline). Foundational + US1 bitince gösterilebilir dilim.
- Sonra US2 (expiry) → US3 (checkout). Her hikaye bağımsız test edilir, artımlı teslim.
- Domain-TDD: Campaign + resolver + apply-skip testleri implementasyondan önce (İLKE VI).
