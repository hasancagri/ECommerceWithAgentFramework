---
description: "Task list for 074 MCP-Only REST Söküm + Catalog Admin Parite"
---

# Tasks: MCP-Only Yüzey — Domain REST Söküm + Catalog Admin Parite

**Input**: Design documents from `/specs/074-mcp-only-rest-teardown/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/catalog-admin-tools.md

**Tests**: Yeni saf-domain davranışı YOK (tüm aggregate metotları mevcut — research R7). İLKE VI zorunlu
test task'ı GEREKMEZ. **Canlı test YOK** (kullanıcı kararı) — doğrulama statik grep + `dotnet build`.

**Organization**: US1 (parite kur) → US2 (söküm; catalog no-twin kaldırma US1'e bağlı) → US3 (statik doğrulama).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel (farklı dosya, bağımsız)
- **[Story]**: US1/US2/US3

---

## Phase 1: Setup

- [X] T001 Baseline: `dotnet build` çalıştır, 0 hata doğrula (söküm öncesi referans nokta)

---

## Phase 2: Foundational (Blocking)

**Purpose**: Yeni tool adları — US1 hem slice hem wrapper bunları okur.

**⚠️ US1 başlayamaz T002 bitmeden.**

- [X] T002 `src/others/Shared/McpToolNames.cs`: `CatalogAdminTools`'a yeni sabitler ekle (create_product,
  set_product_dimensions, set_product_seo, assign_product_tag, remove_product_tag, create_category,
  update_category, create_author, create_product_tag, rename_product_tag, list_product_tags,
  create_specification_attribute, add_specification_attribute_option, list_specification_attributes);
  `StockAdminTools`'a `admin_list_all_stock` (additive; mevcut adlar değişmez)

**Checkpoint**: Ad sözleşmeleri hazır.

---

## Phase 3: User Story 1 - Admin agent catalog'u tam MCP üzerinden yönetir (P1) 🎯 MVP

**Goal**: Catalog admin parite — MCP'de olmayan admin işlevleri `/mcp-admin` tool'u olarak kurulur
(create_product + category/author/tag/spec/dimensions/seo + list'ler + stock list). REST henüz DURUR.

**Independent Test (statik)**: catalog+stock derlenir; yeni Agent slice + McpTools wrapper'lar var; tool
adları `Shared.CatalogAdminTools`/`StockAdminTools` sabitlerinden; anonim `/mcp` filtresi (Program.cs)
değişmemiş (yeni tool'lar yalnız `/mcp-admin` yol-prefix'inde).

**Desen**: Her Agent slice = mevcut `Features/Commands` mantığını taşı + `UserId` param +
`[RequiredScope(CatalogWrite)]` (yazma) + `AdminActionLog.Executed/Rejected` + kısmi-alan (update);
`AdminUpdateProductForAgent` birebir emsal. MCP wrapper = ince sarmalayıcı (`ICurrentUser`→UserId,
`IMessageBus.InvokeAsync`), `[Description]` = kanonik rehber. Opsiyonel param'a `= null` ZORUNLU.

### Products (catalog)

- [X] T003 [P] [US1] `Domains/Products/Features/Agents/AdminCreateProductForAgent.cs` — `Product.Create`
  (ISBN=Id; çakışma→Error, research R1); get-or-create author/publisher; draft doğar; ilk fiyat
  `ProductPriceChange`; yayında değil → event yok
- [X] T004 [P] [US1] `Domains/Products/Features/Agents/AdminSetProductDimensionsForAgent.cs` — `Product.SetDimensions`
- [X] T005 [P] [US1] `Domains/Products/Features/Agents/AdminSetProductSeoForAgent.cs` — `Product.SetSeo`
- [X] T006 [P] [US1] `Domains/Products/Features/Agents/AdminAssignProductTagForAgent.cs` +
  `AdminRemoveProductTagForAgent.cs` — `Product.AddTag` / tag çıkar (mevcut RemoveTag mantığı)
- [X] T007 [US1] `Domains/Products/ProductMcpTools.cs`'e 5 admin wrapper ekle (T003-T006; aynı dosya → seri)

### Categories (catalog)

- [X] T008 [P] [US1] `Domains/Categories/Features/Agents/AdminCreateCategoryForAgent.cs` (`Category.Create`) +
  `AdminUpdateCategoryForAgent.cs` (kısmi: `Rename`/`SetSeo`)
- [X] T009 [US1] `Domains/Categories/CategoryMcpTools.cs`'e 2 admin wrapper ekle (T008)

### Authors (catalog)

- [X] T010 [P] [US1] `Domains/Authors/Features/Agents/AdminCreateAuthorForAgent.cs` (`Author.Create`, normalize get-or-create)
- [X] T011 [US1] `Domains/Authors/AuthorMcpTools.cs`'e create wrapper ekle (T010)

### ProductTags (catalog)

- [X] T012 [P] [US1] `Domains/ProductTags/Features/Agents/` — AdminCreateProductTagForAgent (`Create`),
  AdminRenameProductTagForAgent (`Rename`), AdminListProductTagsForAgent (list; okuma, iz yok)
- [X] T013 [US1] `Domains/ProductTags/ProductTagMcpTools.cs` OLUŞTUR + 3 wrapper (2 yazma + 1 list) (T012)

### SpecificationAttributes (catalog)

- [X] T014 [P] [US1] `Domains/SpecificationAttributes/Features/Agents/` — AdminCreateSpecificationAttributeForAgent
  (`Create`), AdminAddSpecificationAttributeOptionForAgent (`AddOption`), AdminListSpecificationAttributesForAgent (list)
- [X] T015 [US1] `Domains/SpecificationAttributes/SpecificationAttributeMcpTools.cs` OLUŞTUR + 3 wrapper (T014)

### Stock

- [X] T016 [P] [US1] `src/services/stock/Stock.Api/Domains/Stocks/Features/Agents/AdminListAllStockForAgent.cs`
  (GetAllStock mantığı, okuma) + `StockMcpTools.cs`'e `admin_list_all_stock` wrapper

### Doğrulama

- [X] T017 [US1] `dotnet build` (catalog+stock) 0 hata; kod incelemesi: yeni tool'lar `[McpServerTool]` ile
  kayıtlı, Program.cs `ConfigureSessionOptions` yol-prefix filtresi değişmemiş (anonim `/mcp` etkilenmez)

**Checkpoint**: Catalog/stock admin parite tam; REST hâlâ paralel (henüz sökülmedi).

---

## Phase 4: User Story 2 - Domain/iş REST yüzeyi tümüyle kalkar (P1)

**Goal**: catalog/stock/customer-merchant/checkout domain REST + REST-only Command/Query + `.http` +
gateway `catalog-route` sök. Son durum: `Features/Commands|Queries`'te yalnız kritik/iç slice kalır.

**Independent Test (statik)**: quickstart S1 grep'leri boş/beklenen; `dotnet build` 0 hata.

**⚠️ Bağımlılık**: catalog no-twin Command/Query kaldırma (T018-T019) US1 (T003-T015) SONRASI — ikizi
kurulmadan kaldırma yeteneği yok. Twin-mevcut kaldırmalar (T021 stock, T022 customer) US1'den bağımsız.

- [X] T018 [P] [US2] Catalog Products REST sök: `Features/Commands/`'ten UpdateProduct, SetProductPublished,
  CreateProduct, SetProductDimensions, SetProductSeo, AssignTagToProduct, RemoveTagFromProduct sil;
  `Features/Queries/`'ten AdminGetProduct, AdminListProducts, GetProductById, GetProductPriceHistory sil.
  KORU: `ImportBook` (051)
- [X] T019 [P] [US2] Catalog Categories/Authors/Publishers/ProductTags/SpecificationAttributes REST sök:
  Create/Update/Rename Commands + Get* Queries + `*EndpointExtension.cs` sil (aggregate + yeni Agent slice KALIR)
- [X] T020 [US2] `src/services/catalog/Catalog.Api/Program.cs` + ilgili `*EndpointExtension` map çağrılarını
  temizle; `MapMcp("/mcp")` + `MapMcp("/mcp-admin")` + `MapMcpResourceMetadata` DEĞİŞMEZ (T018,T019)
- [X] T021 [P] [US2] Stock REST sök: `Features/Commands/`'ten SetStockQuantity/IncreaseStock/DecreaseStock,
  `Features/Queries/`'ten GetAllStock/GetStockByProductId sil + `StockEndpointExtension` + Program.cs map.
  KORU: CommitStock, RevertCommitStock (checkout saga)
- [X] T022 [P] [US2] Customer merchant-information admin REST sök: `MerchantInformationEndpointExtension`'daki
  `merchant-information` grup + GetMerchantInformation/SetMerchantInformation slice sil. KORU (DOKUNMA):
  `/internal/merchant-key` (GetMerchantKeyInternal) + `/internal/payment-context` (Wallet)
- [X] T023 [P] [US2] Checkout POST `/checkout` sök: `Domains/Checkout/CheckoutEndpointExtension.cs` + Program.cs
  map temizle (broker StartCheckout tetikleyici KALIR)
- [X] T024 [P] [US2] `src/services/gateway/Gateway/appsettings.Development.json`: `catalog-route` sil;
  `ClientCredential` policy başka route kullanmıyorsa temizle (grep ile doğrula). MCP/PRM route'ları KALIR
- [X] T025 [P] [US2] 5 `.http` sil: `Order.http`, `Basket.http`, `Payment.http`, `Catalog.http`, `Customer.http`
- [X] T026 [US2] `dotnet build` (tüm çözüm) 0 hata; ölü GlobalUsings/referansları temizle (T018-T025)

**Checkpoint**: Domain iş REST'i sıfır; yüzey tümüyle MCP; kritik/iç slice ayakta.

---

## Phase 5: User Story 3 - Müşteri + servis akışları bozulmadan sürer (P1)

**Goal**: Söküm hiçbir korunan yolu kırmadı (auth/MCP-infra/S2S/gRPC/saga) — statik + derleme kanıtı.

**Independent Test (statik)**: quickstart S1 grep'leri + kod incelemesi + `dotnet build` PASS.

- [X] T027 [US3] quickstart S1 statik grep'leri: domain iş REST 0, customer'da yalnız `/internal/*`,
  `.http` 0, gateway `catalog-route` 0, `Features/Commands|Queries`'te yalnız kritik/iç (SC-001/006/007)
- [X] T028 [US3] Kod incelemesi: anonim catalog `/mcp` tool seti değişmedi (get_product/search_products/
  get_price_history/list_*); `admin_*` yalnız `/mcp-admin` (Program.cs filtresi) (SC-005)
- [X] T029 [US3] Kod incelemesi: KORUNAN uçlar duruyor — customer `/internal/payment-context` +
  `/internal/merchant-key`, basket gRPC (`basket_items`/`basket_clear`), checkout broker, Identity OIDC,
  Mcp.Gateway MapMcp+PRM, order Process/ReconcileTick (SC-003)
- [X] T030 [US3] Tam `dotnet build` 0 hata (regresyon yok kanıtı)

**Checkpoint**: Söküm sonrası çözüm derlenir; korunan yollar kodda ayakta.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T031 [P] `src/services/catalog/Catalog.Api/FLOW.md`: ürün girişi adımını güncelle (create_product =
  yeni elle giriş yolu; doktrin kayması import-only → import + admin) — İLKE VII, aynı PR
- [ ] T032 [P] `CLAUDE.md` BC haritası + notlar: catalog/stock/customer admin REST→MCP; "058 admin ekranları"
  ve REST admin referanslarını güncelle; müşteri+admin yüzey MCP-only son durumu. AYRICA "Ürün yazım yolu"
  notundaki "elle ürün OLUŞTURMA hâlâ yok" satırını güncelle (create_product ile doktrin kayması: import + admin)
- [ ] T033 Guard'lar: `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` PASS

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002)** → US1 → US2 → US3 → Polish
- **US1 → US2 (catalog no-twin)**: T018-T019 catalog Command/Query kaldırma, US1 T003-T015 (ikiz kurma)
  SONRASI. Twin-mevcut kaldırmalar (T021 stock, T022 customer) US1'den bağımsız.
- **US2 → US3**: statik doğrulama söküm sonrası.
- **US3 → Polish**: doküman + guard en son.

### Within-story paralel

- US1: T003,T004,T005,T006 (Products slice'ları [P]) → T007 seri (aynı McpTools dosyası). Aggregate'ler
  arası T008/T010/T012/T014/T016 tümü [P]. Wrapper task'ları kendi dosyasında seri.
- US2: T018,T019,T021,T022,T023,T024,T025 tümü [P] (farklı servis/dosya) → T020 (catalog Program.cs) →
  T026 (tüm-çözüm build, hepsi sonrası).

---

## Parallel Example: US1 (aggregate slice'ları)

```bash
Task: T003 AdminCreateProductForAgent
Task: T008 AdminCreateCategory + AdminUpdateCategory
Task: T010 AdminCreateAuthor
Task: T012 ProductTag create/rename/list
Task: T014 SpecAttribute create/add-option/list
Task: T016 AdminListAllStock
# sonra her aggregate'in McpTools wrapper task'ı (kendi dosyasında seri)
```

---

## Implementation Strategy

### MVP (US1)

1. T001 → T002 → US1 (T003-T017) → **DUR, DOĞRULA (statik)**: catalog/stock derlenir, parite tool'ları
   kayıtlı. REST hâlâ paralel; hiçbir şey kırılmaz.

### Incremental

2. US2 söküm → S1 statik grep doğrula → yüzey tümüyle MCP.
3. US3 statik + build doğrula (korunan yollar kodda ayakta).
4. Polish: FLOW.md/CLAUDE.md/guard.

---

## Notes

- [P] = farklı dosya, bağımsız.
- Kritik/iç DOKUNMA (research R4): ClearBasketByCheckout, GetBasket(gRPC), CommitStock, RevertCommitStock,
  GetMerchantKeyInternal, ImportBook.
- Yeni scope YOK (CatalogWrite/StockWrite/MerchantCredentialsWrite mevcut — R2).
- Doğrulama STATİK (grep + build + kod incelemesi); canlı chat/Aspire boot YOK (kullanıcı kararı).
- Söküm sırası: önce ikiz (US1) SONRA kaldır (US2) — korunan yol hiç boşta kalmaz.
- Her mantıksal grup sonrası commit.