# Research — 074 MCP-Only REST Söküm + Catalog Parite

Phase 0. Spec'teki açık kararlar + söküm/çevirme sınırının netleştirilmesi.

## R1 — `create_product` ISBN çakışması

**Decision** (DÜZELTİLDİ — kod incelemesi): ISBN mağazada `Product.Gtin`'de yaşar (indexli), ProductId
rastgele Guid'dir (`Product.Create` — AggregateRoot). `ImportBook` idempotency'yi Gtin-query ile kurar
(Id=ISBN DEĞİL). Dolayısıyla `create_product`: `session.Query<Product>().FirstOrDefault(p => p.Gtin == isbn)`
ile bakar; VARSA **Error** (çoğaltma yok, agent kullanıcıya admin_update_product önerir); YOKSA
`Product.Create(...)` + `SetIdentifiers(isbn, gtin: isbn, null)`. ISBN zorunlu param (künye kimliği).
**Draft doğar** (spec FR-009): eski REST CreateProduct auto-`Publish()` ederdi; yeni tool ETMEZ —
`admin_set_published` ayrı adım. Fiyat>0 ise geçmişin ilk satırı (`ProductPriceChange`, OldPrice=null).
Draft olduğu için `ProductChangedEvent` YAYILMAZ (AdminUpdateProduct deseni: yalnız Published'da event).

**Rationale**: İki yazım yolu (import + admin) aynı kimliği paylaşır; sessiz üzerine-yazma veri kaybı
riski. Hata = güvenli varsayılan, agent kullanıcıya "bu ISBN zaten var, güncellemek için admin_update_product"
diyebilir.

**Alternatives**: (a) upsert — reddedildi (import feed'i sessizce ezebilir); (b) rastgele Guid Id —
reddedildi (051 ISBN=Id sözleşmesini bozar).

## R2 — Yeni scope gerekli mi?

**Decision**: HAYIR. Yeni yazma tool'ları mevcut `AuthorizationScopes.CatalogWrite` kullanır (070
`AdminUpdateProduct`/`AdminSetPublished` ile aynı). Stock zaten `StockWrite`, customer merchant zaten
`MerchantCredentialsWrite`. `KnownScopes` registry'sine ekleme YOK.

**Rationale**: İLKE V — scope kapalı registry; catalog admin yazma tek scope altında toplanmış. Alt-tool
başına scope bölmek gereksiz granülerlik (rol = scope demeti; admin rolü CatalogWrite'ı zaten taşır).

**Alternatives**: `catalog.category.write` vb. ince scope — reddedildi (YAGNI; mevcut model yeterli).

## R3 — Okuma admin tool'ları koruması (`list` tool'ları)

**Decision**: Yeni catalog admin LIST tool'ları (`admin_list_specification_attributes`,
`admin_list_product_tags`) `/mcp-admin` ucunda yayınlanır (yol-prefix filtresi), okuma olduğu için
`[RequiredScope]` handler'da GEREKMEZ ama `/mcp-admin` login-korumalı (070 deseni: admin okuma tool'ları
= `AdminListProducts`/`AdminGetProduct` gibi, scope'suz ama korumalı uçta). AdminActionLog YAZMAZ (okuma).

**Rationale**: 070 emsali — admin okuma tool'ları korumalı uçta yaşar, iz bırakmaz; yazma tool'ları scope + iz.

## R4 — Çevir / Sök / Koru envanteri (kesin liste)

Mevcut Agent ikizine göre (kod taraması):

**Agent ikizi VAR → yalnız REST endpoint + REST-only Command/Query sil:**
- catalog: `UpdateProduct`, `SetProductPublished`, `AdminGetProduct`, `AdminListProducts`,
  `GetProductById`(→`GetProduct`), `GetProductPriceHistory`, `GetAuthors`(→`ListAuthors`),
  `GetCategories`(→`ListCategories`), `GetPublishers`(→`ListPublishers`)
- stock: `SetStockQuantity`(→`AdminSetStock`), `Increase`/`Decrease`(→`AdminAdjustStock`),
  `GetStockByProductId`(→agent `GetStockByProductId`)
- customer: `SetMerchantInformation`(→`AdminSetMerchantCredentials`),
  `GetMerchantInformation`(→`AdminGetMerchantStatus`)

**Agent ikizi YOK → önce Agent slice + tool kur, sonra sök:**
- catalog Products: `CreateProduct`→`admin_create_product`, `SetProductDimensions`→`admin_set_product_dimensions`,
  `SetProductSeo`→`admin_set_product_seo`, `AssignTagToProduct`→`admin_assign_product_tag`,
  `RemoveTagFromProduct`→`admin_remove_product_tag`
- catalog Categories: `CreateCategory`→`admin_create_category`, `UpdateCategory`→`admin_update_category`
- catalog Authors: `CreateAuthor`→`admin_create_author`
- catalog ProductTags: `CreateProductTag`→`admin_create_product_tag`,
  `RenameProductTag`→`admin_rename_product_tag`, `GetProductTags`→`admin_list_product_tags`
- catalog SpecificationAttributes: `CreateSpecificationAttribute`→`admin_create_specification_attribute`,
  `AddSpecificationAttributeOption`→`admin_add_specification_attribute_option`,
  `GetSpecificationAttributes`→`admin_list_specification_attributes`

**Kritik/İç (DOKUNMA):**
- basket `ClearBasketByCheckout` (checkout saga, BasketEventHandlers/IMessageBus)
- basket `GetBasket` (gRPC `BasketItemsGrpcService` → order charge)
- stock `CommitStock`, `RevertCommitStock` (CheckoutProcess sağası publish, StockEventHandlers tüketir)
- customer `GetMerchantKeyInternal` (S2S `/internal/merchant-key`)
- catalog `ImportBook` (051 import)

## R5 — `GetAllStock` (Agent ikizi yok, admin okuma)

**Decision**: `admin_list_all_stock` Agent tool'u KUR (`/mcp-admin`, okuma). Admin stok genel görünüm
ister; parite için gerekli. Küçük — mevcut query mantığı Agent slice'a taşınır.

**Rationale**: "Kullanıcı/admin yüzeyli hiçbir Command/Query kalmaz" (SC-007) — atmak yerine çevir.

**Alternatives**: At (admin get_stock tek-ürün yeter) — reddedildi (envanter genel görünüm kaybı).

## R6 — Gateway `catalog-route` + ClientCredential

**Decision**: `catalog-route` (REST proxy) + `catalog-mcp`/`catalog-mcp-admin` route'ları AYRI.
Yalnız `catalog-route` (REST `{version}/catalogs/**`) silinir; MCP route'ları KALIR. `ClientCredential`
authorization policy yalnız `catalog-route`'ta kullanılıyorsa tanımı da temizlenir; başka route
kullanıyorsa BIRAKILIR (plan'da grep ile doğrula, tasks'ta guard).

**Rationale**: MCP proxy'leri müşteri/admin yüzeyi — korunur. Yalnız yetim REST proxy düşer.

## R7 — Domain-TDD kapsamı

**Decision**: Yeni saf-domain davranışı YOK (tüm aggregate metotları mevcut: `Product.Create/SetDimensions/
SetSeo/AddTag`, `Category.Create/Rename`, `Author.Create`, `ProductTag.Create/Rename`,
`SpecificationAttribute.Create/AddOption`). Test-first task gerekmez; iş = handler/tool wiring + söküm →
test-sonra + `quickstart.md` canlı doğrulama.

**Rationale**: İLKE VI kapsamı saf domain; bu feature davranış eklemez, yüzey taşır.