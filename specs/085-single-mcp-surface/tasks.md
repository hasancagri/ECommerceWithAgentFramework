# Tasks: Tek MCP Yüzeyi — /mcp-admin Sökümü + Scope-Bazlı Tool Budaması

**Input**: Design documents from `/specs/085-single-mcp-surface/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R6), contracts/mcp-surface.md, quickstart.md

**Tests**: İLKE VI — saf mantık (ScopeResolver genişletmesi, scope-budama filtresi) test-first; endpoint/wiring canlı doğrulama (quickstart S1-S5).

**Organization**: US1 = admin tek uçtan çalışır; US2 = müşteri/anonim sızıntısız; US3 = çift-kayıt yan yana. Mekanizma ortak olduğundan US1 ana gövdeyi taşır; US2/US3 doğrulama + kalan uçlar.

## Phase 1: Setup

- [X] T001 Dal `085-single-mcp-surface` (store) + AgentPlatform reposunda `085-scope-ceiling` dalı aç; her ikisinde baseline `dotnet build` + `dotnet test` yeşil kaydet.

## Phase 2: Foundational (blocking)

**AgentPlatform (AYRI PR — ÖNCE merge; R3):**

- [X] T002 [P] TEST-FIRST: `../AgentPlatform/src/Identity.Server.Tests/.../ScopeResolverTests.cs` — clientCeiling kesişimi: tavan-üstü talep elenir (hata yok), tavan∩rol∩talep doğru, alwaysAllow tavandan bağımsız, boş-talep davranışı. Kırmızı koş.
- [X] T003 `../AgentPlatform/src/Identity.Server/Rbac/ScopeResolver.cs` — `Resolve(requested, roleBundle, clientCeiling, alwaysAllow)`; T002 yeşil.
- [X] T004 `../AgentPlatform/src/Identity.Server/Connect/AuthorizeEndpoint.cs` — application kaydından izinli scope setini oku (`IOpenIddictApplicationManager`), `ScopeResolver`'a clientCeiling geçir; token exchange yolunda da aynı kesişim.
- [X] T005 `../AgentPlatform/src/Identity.Server/Program.cs` — OpenIddict scope-izin ön-validasyonunu gevşet (`IgnoreScopePermissions`); tavanın artık YALNIZ ScopeResolver'da zorlandığını yorumla değil holder/FLOW ile belgele.
- [X] T006 `../AgentPlatform/src/Identity.Server/FLOW.md` — scope-açılım adımını yeni kesişim formülüyle güncelle (aynı PR, İLKE VII).
- [ ] T007 AgentPlatform canlı smoke: müşteri istemcisiyle union scope talebi → bağlantı kırılmaz, token müşteri demetiyle; DCR istemcisi + admin kullanıcı → admin scope YOK. PR aç/merge et.

**ECommerce ortak taban:**

- [X] T008 [P] TEST-FIRST: `tests/Mcp.Gateway.Tests/ScopePruningTests.cs` (veya Common test projesi) — saf budama kuralı: `tool ∉ adminNames ∨ requiredScope ∈ tokenScopes`; kısmi scope, token'sız, bilinmeyen tool senaryoları. Kırmızı koş.
- [X] T009 `src/others/Common/Extensions/McpScopePruningExtension.cs` (yeni) — scope-bazlı `ConfigureSessionOptions` yardımcı: `(adminToolScopeMap, ClaimsPrincipal) → tool seti filtresi`; T008 yeşil. `AddMcpAdminResourceMetadata`'yı emekliye ayıracak zemin.
- [X] T010 [P] 4 BC `*AdminSurface` holder'ına tool→scope eşlemesi ekle: `Catalog.Api/Mcp/CatalogAdminSurface.cs` (22 tool → catalog.read/catalog.write ayrımı), `Stock.Api/Mcp/StockAdminSurface.cs`, `Customer.Api/Mcp/CustomerAdminSurface.cs`, `Discount.Api/Mcp/DiscountAdminSurface.cs` — kaynak `[RequiredScope]` attribute'larıyla birebir.

## Phase 3: US1 — Admin tek uçtan tüm yetkili tool'larını görür (P1)

**Goal**: `/mcp-admin` ölür; admin tek `/mcp`'den her şeyi yapar. **Independent test**: quickstart S1.

- [X] T011 [P] [US1] `src/services/catalog/Catalog.Api/Program.cs` — ikinci `MapMcp("/mcp-admin")` + `AddMcpAdminResourceMetadata` sil; `ConfigureSessionOptions` T009 yardımcısıyla scope-bazlı; `/mcp` anonim kalır.
- [X] T012 [P] [US1] `src/services/stock/Stock.Api/Program.cs` — aynı söküm + scope-bazlı budama; `/mcp` anonim kalır.
- [X] T013 [P] [US1] `src/services/customer/Customer.Api/Program.cs` — aynı söküm; `/mcp` `RequireAuthorization` kalır; merchant-admin tool'ları scope'la ayrışır; `/mcp` PRM `scopes_supported`'ına `merchant.credentials.write` eklenir (analiz C2 — R6 "tam demet" vaadi).
- [X] T014 [P] [US1] `src/services/discount/Discount.Api/Program.cs` — R5: tek uç `/mcp-admin`→`MapMcp("/mcp").RequireAuthorization()`; scope yoksa boş liste; `/mcp` slug'ında kendi PRM'i açılır (`discount.admin.write` ilanı — eski `mcp-admin/discount` PRM'inin yerine, analiz C2).
- [X] T015 [US1] `src/agents/Mcp.Gateway/FacadeScopes.cs` — Customer/Admin ayrımı → tek `All` union sabiti. DİKKAT (analiz C1): union SEED İSTEMCİ izin setleriyle karşılaştırılarak kurulur — bugünkü Customer listesinde OLMAYAN `reviews.write` + `library.read` + `library.write` EKLENİR (079 tuzağı: PRM'de ilan edilmeyen scope istenmez → audience eksik → 401). Tuzak yorumları veriyle taşınır.
- [X] T016 [US1] `src/agents/Mcp.Gateway/Aggregation/ToolCatalogCollector.cs` — cache anahtarı scope-parmakizi (R2); keşif oturum token'ıyla; m2m TAM-katalog taraması ayrı cache'te yönlendirme registry'sini besler.
- [X] T017 [US1] `src/agents/Mcp.Gateway/Program.cs` — `MapMcp("/mcp-admin")` + ikinci PRM sil; tek PRM `scopes_supported=FacadeScopes.All`; JWT challenge tek liste; `CurrentSurface`/`SurfaceFilter` bağımlılığı sök.
- [X] T018 [US1] `src/agents/Mcp.Gateway/Routing/SurfaceFilter.cs` SİL + `Options/FacadeOption.cs`'ten `DownstreamBc.Surface` alanını çıkar; `ProxyToolInvoker`/`McpStepUpMiddleware` çağrı imzalarını uyarla (step-up davranışı: registry `RequiresUserAuth` aynen).
- [X] T019 [US1] `src/agents/Mcp.Gateway/appsettings*.json` — `*-admin` Downstream entry'leri sil (BC başına tek entry, discount `McpUrl=/mcp`); `src/services/gateway/Gateway/appsettings*.json` — `/mcp-admin/{service}` rotaları + admin PRM kayıtları sil.
- [X] T020 [US1] Build guard: tüm çözüm + bağımlı TEST projeleri derlenir (`dotnet build`); `tests/Mcp.Gateway.Tests/SurfaceFilterTests.cs` sil, `ScopePruningTests` + collector cache-key testleri yeşil (`dotnet test`).
- [ ] T021 [US1] Canlı doğrulama quickstart S1: admin kaydı (`/mcp?client=admin`) → 33 admin tool + müşteri seti listede; `admin_adjust_stock` başarılı + `AdminActionLog`; tüm eski `/mcp-admin` uçları 404.

## Phase 4: US2 — Müşteri/anonim admin tool'u ne görür ne çağırır (P1)

**Goal**: Sızıntı 0; 403 son savunma. **Independent test**: quickstart S2 + S4 + S5.

- [X] T022 [US2] ~~`src/agents/Mcp.Gateway/Auth/McpStepUpMiddleware.cs` — `/mcp-admin` dalını sök, challenge scope listesini müşteri demetiyle bırak (uyuyan kod, davranış değişmez); yorum güncelle.~~ SUPERSEDED (kullanıcı kararı): upfront login kalıcı — dosya tümüyle SİLİNDİ (anonim/step-up modu bir daha açılmayacak); `RequireLoginUpfront`/`RequiresUserAuth` alanları da kaldırıldı.
- [ ] T023 [US2] Canlı S2: test müşterisiyle bağlan (union talep → kısıtlı token, bağlantı sağlam); listede admin tool 0; ham `tools/call` ile `admin_set_published` → tool-error + durum değişmedi; anonim `catalog-api/mcp` seti 070 ile birebir.
- [ ] T024 [US2] Canlı S4 (kısmi admin): geçici `catalog-manager` rolü (`catalog.read`+`catalog.write`) → yalnız catalog admin tool'ları görünür; `admin_set_stock` çağrısı tool-error.
- [ ] T025 [US2] Canlı S5 (DCR tavanı): DCR istemcisi + admin kullanıcı login → token'da admin scope yok, admin çağrı tool-error (SC-003).

## Phase 5: US3 — Admin + test kullanıcı yan yana (P2)

**Goal**: Çift Desktop kaydı, kimlik karışması 0. **Independent test**: quickstart S3.

- [X] T026 [US3] Kayıt konvansiyonunu belgele: `contracts/mcp-surface.md` zaten tanımlıyor; kullanıcı-yüzü not olarak CLAUDE.md mcp-gateway satırına `?client=admin` cache-ayrıştırma işaretini ekle.
- [ ] T027 [US3] Canlı S3: iki kayıt eşzamanlı (admin+test müşterisi); sepet müşteride, `AdminActionLog` admin'de; `~/.mcp-auth`ta iki ayrı hash dosyası.

## Phase 6: Polish & Cross-Cutting

- [X] T028 [P] CLAUDE.md güncelle: BC haritası (catalog/stock/customer/discount + mcp-gateway satırları `/mcp-admin`→tek `/mcp` scope-budamalı), 070/074 bahisleri (allowlist tuzağı → tool→scope holder kuralı), "Admin yüzeyi MCP'de" bloğu.
- [X] T029 [P] Eski doküman süprüntüsü: `Common/Extensions/McpResourceMetadataExtension.cs`'te kullanılmayan `AddMcpAdminResourceMetadata` sök; repo içi `/mcp-admin` referans taraması (`grep -r "mcp-admin" src/ tests/`) sıfırlanır (specs/ hariç).
- [X] T030 Regresyon turu: tam `dotnet build` + `dotnet test` (YEŞİL), `scripts/check-flow-links.sh` (YEŞİL); canlı Aspire: arama→sepet ucu-uca doğrulandı (customer scope-budama + basket write PASS, Hyperion sepete eklendi). `start_payment`→Confirmed PAS GEÇİLDİ — PG tarafında ciddi yeniden düzenleme planlı, o bitince ayrı doğrulanacak. Memory notu güncelle (D8/tek-uç backlog kapanışı).

## Dependencies

- T001 → T002-T010 (Foundational) → US1 (T011-T021) → US2 (T022-T025) → US3 (T026-T027) → Polish (T028-T030).
- AgentPlatform zinciri T002→T003→T004→T005→T006→T007; T007 merge OLMADAN T017 (union PRM) canlıya çıkamaz — kod yazılabilir, S1/S2 doğrulaması T007'ye bağımlı.
- T008→T009→T010; T011-T014 hem T009'a hem T010'a bağımlı.
- US2 fiilen US1 koduyla gelir; T022-T025 US1 merge'inden sonra bağımsız doğrulanır. US3 yalnız doğrulama+doküman.

## Parallel Execution Examples

- T002 ‖ T008 (iki repo, bağımsız test-first).
- T011 ‖ T012 ‖ T013 ‖ T014 — 4 BC, 4 paralel agent (1-servis-1-agent deseni onaylı).
- T028 ‖ T029 (farklı dosyalar).

## Implementation Strategy

MVP = Foundational + US1 (T001-T021): admin tek uçta çalışır, eski uçlar ölür — tek başına teslim edilebilir. US2 görevleri ağırlıkla doğrulama (mekanizma US1'de); US3 sıfır sunucu kodu. AgentPlatform PR'ı küçük ve öne alınmış — riskin en yüksek olduğu yer (IdP davranış değişimi), en erken kapatılır.
