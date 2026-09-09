# Tasks: Admin Yüzeyinin MCP'ye Taşınması (070)

**Input**: Design documents from `/specs/070-admin-mcp-surface/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R9), data-model.md, contracts/

**Tests**: Yeni saf domain davranışı beklenmiyor (slice'lar mevcut aggregate metotlarını çağırır) —
İLKE VI görevi yalnız T017'de koşullu. Diğer katmanlar canlı doğrulama (quickstart.md).

**Organization**: Fazlar user story'ye göre; her story bağımsız teslim edilebilir dilim.

## Path Conventions

Mikroservis (plan.md yapısı): `src/services/<bc>/<Bc>.Api/`, `src/others/`, `src/agents/`.

---

## Phase 1: Setup

**Purpose**: Tek-kaynak sabitler + çalışma dalı

- [X] T001 Branch `070-admin-mcp-surface` aç (master'dan)
- [X] T002 `src/others/Shared/McpToolNames.cs`'e yeni tool-adı sınıfları ekle: `CatalogAdminTools`
  (admin_list_products, admin_get_product, admin_update_product, admin_set_published,
  admin_get_price_history), `StockAdminTools` (admin_set_stock, admin_adjust_stock),
  `CustomerAdminTools` (admin_get_merchant_status, admin_set_merchant_credentials,
  admin_submit_onboarding, admin_onboarding_status), `OrderTools.QuoteInstallments`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Kimlik + gateway — tüm story'lerin OAuth/erişim ön şartı

- [X] T003 [P] Identity.Server `Config.cs` + `SeedHostedService`: `external-admin-agent` seed istemcisi
  (contracts/seeded-admin-client.md sözleşmesi: public+PKCE, Claude callback + loopback redirect,
  Implicit consent, scope tavanı openid/profile/storefront.read/catalog.write/stock.write/
  merchant.credentials.write). `ExternalAgentDefaults`/`DcrRequestValidator` DOKUNULMAZ
- [X] T004 [P] Gateway (`src/services/gateway/Gateway/`): `/mcp-admin/{catalog|stock|customer}` proxy
  rotaları + bu uçların RFC 9728 PRM (resource metadata) rotaları (mevcut `/mcp/*` rota deseninin ikizi)

**Checkpoint**: Seed istemci token verebiliyor; gateway admin-MCP rotaları 404 değil (uçlar henüz yoksa 502 kabul)

---

## Phase 3: US1 — Admin Claude Desktop'tan ürün yönetir (P1) 🎯 MVP

**Goal**: Catalog admin işlemleri (liste/detay/künye/yayın/fiyat-geçmişi) korumalı `/mcp-admin`
tool'larıyla; her yazma AdminActionLog izli.

**Independent Test**: quickstart.md §2-3 — OAuth challenge→login→5 tool listelenir; liste→detay→
güncelle→yayından kaldır→geçmiş zinciri; customer rolüyle 403; DCR'dan catalog.write alınamaz.

- [X] T005 [US1] `src/services/catalog/Catalog.Api/AdminAudit/AdminActionLog.cs`: salt-append Marten
  dokümanı (data-model.md alanları) + Executed/Rejected fabrikaları (AgentQueryLog emsali)
- [X] T006 [P] [US1] `Domains/Products/Features/Agents/AdminListProductsForAgent.cs`: sayfalı+aramalı
  liste slice'ı (AdminListProducts ikizi; yayında olmayan dahil; `[RequiredScope(CatalogWrite)]`)
- [X] T007 [P] [US1] `Domains/Products/Features/Agents/AdminGetProductForAgent.cs`: tam künye + bağlar
  + yayın durumu tek yanıt (AdminGetProduct ikizi)
- [X] T008 [P] [US1] `Domains/Products/Features/Agents/AdminUpdateProductForAgent.cs`: tek-ürün künye
  güncelleme (UpdateProduct ikizi; opsiyonel parametrelere DEFAULT — MCP tuzağı); yanıt GÜNCEL ürün;
  AdminActionLog yazımı; mevcut ProductPriceChange/ProductChangedEvent akışı bozulmaz
- [X] T009 [P] [US1] `Domains/Products/Features/Agents/AdminSetPublishedForAgent.cs`: yayın anahtarı
  (SetProductPublished ikizi) + AdminActionLog
- [X] T010 [P] [US1] `Domains/Products/Features/Agents/AdminGetPriceHistoryForAgent.cs`: fiyat geçmişi
  (GetProductPriceHistory ikizi)
- [X] T011 [US1] `Domains/Products/ProductMcpTools.cs`: ayrı `[McpServerToolType]` admin sınıfı — 5 tool
  sarmalayıcı; Description'lar contracts/admin-mcp-tools.md kalitesinde (alan+değer+örnek; FR-015)
- [X] T012 [US1] Catalog `Program.cs`: `MapMcp("/mcp-admin").RequireAuthorization()` + admin tool'ların
  YALNIZ bu uçta yayını (anonim `/mcp` tool seti DEĞİŞMEZ) + `AddMcpResourceMetadata` (catalog.write)
- [X] T013 [US1] Canlı doğrulama: quickstart.md §2-3 (OAuth akışı + zincir + negatifler + catalogDb izi)

**Checkpoint**: US1 tek başına gösterilebilir — MVP

---

## Phase 4: US2 — Admin stok yönetir (P2)

**Goal**: Stok mutlak set + artır/azalt, korumalı `/mcp-admin` tool'larıyla, izli.

**Independent Test**: quickstart.md §4 — 25 set→22 azalt→negatif reddi; stockDb izi.

- [X] T014 [P] [US2] `src/services/stock/Stock.Api/AdminAudit/AdminActionLog.cs` (T005 deseni; bilinçli tekrar)
- [X] T015 [P] [US2] `Domains/Stocks/Features/Agents/AdminSetStockForAgent.cs`: mutlak set
  (SetStockQuantity ikizi; `[RequiredScope(StockWrite)]`) + iz
- [X] T016 [US2] `Domains/Stocks/Features/Agents/AdminAdjustStockForAgent.cs`: artır/azalt (058 mevcut
  domain metotları; negatif reddi domain'de kalır) + iz
- [X] T017 [US2] KOŞULLU (İLKE VI): `ProductStock`'ta adjust için domain metodu EKSİKSE önce
  `tests/Stock.Api.Tests/`e failing test, sonra metot; varsa bu görev atlanır
- [X] T018 [US2] Stock `Program.cs` + `StockMcpTools.cs`: `/mcp-admin` ucu + metadata (stock.write) +
  2 admin tool sarmalayıcı (anonim `/mcp` get_stock DEĞİŞMEZ); canlı doğrulama quickstart §4

**Checkpoint**: US1+US2 birlikte 058 ekranının künye+stok parçasını tam karşılar

---

## Phase 5: US3 — Merchant kimlik + onboarding sarmalayıcı (P3)

**Goal**: Merchant kimlik durum/kayıt + PG onboarding submit/status, Customer `/mcp-admin`'de, izli.

**Independent Test**: quickstart.md §5 — maskeli durum; kaydet→çekim yeni key'le; başvuru→Pending→durum;
PG kapalıyken dostane hata.

- [X] T019 [P] [US3] `src/services/customer/Customer.Api/AdminAudit/AdminActionLog.cs` (T005 deseni)
- [X] T020 [P] [US3] `Domains/MerchantInformations/Features/Agents/AdminGetMerchantStatusForAgent.cs`:
  maskeli durum (GetMerchantInformation ikizi; key ASLA dönmez; `[RequiredScope(MerchantCredentialsWrite)]`)
- [X] T021 [P] [US3] `Domains/MerchantInformations/Features/Agents/AdminSetMerchantCredentialsForAgent.cs`:
  upsert (SetMerchantInformation ikizi); izde/yanıtta key düz metin YOK (Summary: "credentials rotated")
- [X] T022 [US3] `Onboarding/` altına PG Merchant.Api MCP istemcisi: ChatAgent
  `OnboardingGatewayTokenHandler` client_credentials deseni Customer.Api'ye taşınır (named client,
  resilience-muaf, makine token'ı); ANAYASA SAPMASI yorumu koda yazılır (plan Complexity referansı)
- [X] T023 [US3] `Features/Agents/AdminSubmitOnboardingForAgent.cs` + `AdminOnboardingStatusForAgent.cs`:
  sarmalayıcı slice'lar (contracts/admin-mcp-tools.md parametreleri; submit izli; PG hatası dostane) —
  ChatAgent admin persona'sı DOKUNULMAZ (paralel yaşar)
- [X] T024 [US3] Customer `Program.cs` + yeni `MerchantAdminMcpTools`: `/mcp-admin` ucu + metadata
  (merchant.credentials.write) + 4 tool (mevcut korumalı `/mcp` müşteri tool seti DEĞİŞMEZ); canlı §5

**Checkpoint**: Docker-reset kurtarma yolu tamamen ekransız çalışır

---

## Phase 6: US4 — Müşteri taksit sorgusu (P4)

**Goal**: `quote_installments` Order.Api `/mcp`'de; PlaceOrder zincirinin quote'a kadar aynısı + A2A.

**Independent Test**: quickstart.md §6 — seçenekler chat kural-8 sonuçlarıyla AYNI; boş sepet dostane hata.

- [X] T025 [US4] Order.Api'ye `A2A` paket referansı (sürüm Directory.Packages.props'ta MEVCUT; csproj
  sürümsüz) + `A2A/PaymentAgentQuoteClient.cs`: PG `quote-installments` skill çağrısı (ChatAgent
  PaymentAgentInstallmentTool deseninden uyarlanır; named client resilience-muaf, timeout cömert)
- [X] T026 [US4] `Domains/Orders/Features/Agents/QuoteInstallmentsForAgent.cs`: basket gRPC toplam +
  payment-context S2S + A2A quote; yanıt yalnız {installmentNumber,totalPrice} (contracts/
  customer-agent-tools.md); boş sepet/kart-yok/PG-yok dostane Result kodları
- [X] T027 [US4] `OrderMcpTools.cs` + Order `Program.cs`: `quote_installments` sarmalayıcı (mevcut
  korumalı `/mcp`; scope order.read+payment.read) + A2A named-client kaydı; canlı doğrulama §6

**Checkpoint**: Dış agent taksit görebiliyor — ChatAgent sökümünde müşteri kaybı sıfır

---

## Phase 7: US5 — Playbook göçü (P5)

**Goal**: 069 sorgu rehberi kanonik evine (tool description) iner; guard yeni evi denetler.

**Independent Test**: quickstart.md §7 — TEMİZ dış-agent oturumunda temalı/benzerlik/sayfalama
senaryoları + eval 13/13; ChatAgent regresyonsuz.

- [X] T028 [US5] `Storefront.Api/Domains/StorefrontView/StorefrontMcpTools.cs`: `[Description]`'a
  SADELEŞTİRİLMİŞ playbook (şema kolonları + sorgu kalıpları + {{EMBED}} + 0.68 + sayfalama + dürüstlük/
  grounding; ChatAgent-özgü persona satırları GİRMEZ — R7). ChatAgent prompt kopyası DONDURULUR (dokunulmaz)
- [X] T029 [US5] `scripts/check-agent-query-schema.sh`: `prompt_file` → `src/services/storefront/
  Storefront.Api/Domains/StorefrontView/StorefrontMcpTools.cs`; script yeşil
- [ ] T030 [US5] Temiz-oturum eval: 069 eval setinin 13 senaryosu dış-agent muadili olarak koşulur
  (quickstart §7); ChatAgent chat'inde örneklem regresyon kontrolü (FR-013)

**Checkpoint**: Keşif kalitesi artık istemciden bağımsız — söküm ön şartı tamam

---

## Phase 8: Polish & Cross-Cutting

- [X] T031 `dotnet build && dotnet test` + tüm guard'lar (`check-agent-query-schema.sh`,
  `check-claude-spec-links.sh`, `check-flow-links.sh`) yeşil
- [ ] T032 quickstart.md §8 kapanış: SC-001 tam tur (058'in her işlemi yalnız Claude Desktop'tan) +
  anonim `/mcp` regresyonu + SC-002/005/006 negatifleri
- [X] T033 CLAUDE.md güncelle: BC haritasında catalog/stock/customer satırlarına `/mcp-admin` + admin
  tool notu; storefront satırına "playbook tool description'da"; "Müşteri yüzeyi MCP-only" bölümüne
  admin durumu; 070 spec yolu Origin-dışı feature olarak ilgili satırlara
- [X] T034 Memory notu: 070 durumu + tuzaklar (anonim uçta challenge yok bulgusu, A2A-servis borcu,
  çift playbook dondurması) + 071 söküm ön şartlarının hepsi tamam işareti

---

## Dependencies & Execution Order

- **Phase 1 → 2 → (3|4|5|6|7)**: Setup + Foundational hepsinin ön şartı.
- **US1-US5 birbirinden BAĞIMSIZ** — farklı BC/dosyalar; sıra öncelik sırası (P1→P5) önerilir ama
  paralel yürütülebilir. US4 ve US5'in Foundational'a gerçek bağımlılığı bile yok (mevcut uçlar) —
  T002 sonrası başlayabilirler.
- **Phase 8** tüm story'lerden sonra.

## Parallel Example

- T003 ‖ T004 (Identity vs Gateway).
- US1 içinde T006-T010 beş slice paralel (ayrı dosyalar); T011-T012 onları bekler.
- Story düzeyinde: US2 (T014-T018) ‖ US3 (T019-T024) ‖ US4 (T025-T027) ‖ US5 (T028-T030).

## Implementation Strategy

**MVP = Phase 1+2+US1** (admin OAuth + ürün yönetimi): vizyonun kanıtı tek fazda. Sonra artımlı:
US2 (stok) → US3 (merchant) → US4 (taksit) → US5 (playbook) → Polish. Her checkpoint'te canlı
doğrulama; söküm (071) ancak T032 yeşilken açılır.
