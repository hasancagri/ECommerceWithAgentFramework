# Tasks: Dış Agent MCP Erişimi (OAuth)

**Input**: Design documents from `/specs/061-external-mcp-oauth/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R8), data-model.md, contracts/, quickstart.md

**Tests**: İlke VI gereği saf birim (DcrRequestValidator) test-first; handler/endpoint/wiring
test-sonra/canlı doğrulama. E2E kanıt quickstart.md senaryolarıyla.

**Organization**: US1 = P1 ekransız alışveriş (MVP), US2 = P2 kayıtla giriş, US3 = P3 sessiz
yenileme + regresyon.

## Phase 1: Setup

- [X] T001 Feature branch aç: `git checkout -b 061-external-mcp-oauth` (master temiz olmalı)

---

## Phase 2: Foundational (tüm hikâyeleri bloklar)

**Purpose**: Keşif + istemci kaydı + consent altyapısı — OAuth zincirinin gövdesi.

- [X] T002 [P] Dış-agent scope demeti sabitini tanımla (storefront.read, basket.read/write,
      order.read/write, customer.read, payment.read + openid/profile/email/offline_access) —
      `src/others/Identity.Server/Connect/ExternalAgentDefaults.cs` (data-model listesi tek kaynak)
- [X] T003 [P] **TEST-FIRST**: `DcrRequestValidator` birim testleri (redirect URI kalıpları:
      localhost/127.0.0.1/claude callback kabul, diğerleri red; grant/auth-method/scope süzgeci;
      contracts/dcr-register.md kuralları) — `tests/Identity.Server.Tests/DcrRequestValidatorTests.cs`
      (proje yoksa xUnit+Shouldly ile aç, çözüme ekle)
- [X] T004 T003'ü geçirecek saf `DcrRequestValidator` sınıfı —
      `src/others/Identity.Server/Connect/DcrRequestValidator.cs`
- [X] T005 RFC 7591 DCR ucu `POST /connect/register`: validator → `IOpenIddictApplicationManager`
      ile public client (PKCE, auth_code+refresh, ConsentType=Explicit, demet-içi scope izinleri) —
      `src/others/Identity.Server/Connect/RegisterEndpoint.cs` + `Program.cs` map
- [X] T006 [P] Discovery dokümanına `registration_endpoint` alanını ekle (OpenIddict event
      handler) + revocation endpoint'ini aç — `src/others/Identity.Server/Program.cs`
- [X] T007 Consent sayfası (scope listesi + Onayla/Reddet) ve authorize akışında Explicit
      client'lar için consent adımı; Reddet → error dönüşü, yan etkisiz —
      `src/others/Identity.Server/` (mevcut authorize yapısına uygun: Pages/Views + Connect handler)
- [X] T008 [P] `Common`'a MCP keşif extension'ı: `/.well-known/oauth-protected-resource` ucu
      (resource forwarded-header'dan, authorization_servers, scopes_supported) + JwtBearer
      challenge'ına `resource_metadata` + `scope` parametreleri —
      `src/others/Common/Extensions/McpResourceMetadataExtension.cs` (+ AuthenticationExtension.cs)
- [X] T009 [P] Gateway'e 4 korumalı servisin well-known route'ları
      (`/.well-known/oauth-protected-resource/mcp/<servis>` → servis) —
      `src/services/gateway/Gateway/appsettings.Development.json`

**Checkpoint**: `curl` ile 401+challenge, metadata dokümanı, discovery'de registration_endpoint
görülebilir (quickstart §1) — Claude'suz zincir ayakta.

---

## Phase 3: User Story 1 — Mevcut kullanıcı agent'ıyla alışveriş (P1) 🎯 MVP

**Goal**: Claude Code bağlanır, bir kez tarayıcı login+consent, yazışmayla ara→sepet→sipariş→görüntüle.

**Independent Test**: quickstart §2–3 (bağlan + E2E alışveriş).

- [X] T010 [US1] 4 korumalı serviste MapMcp'yi korumaya al + keşif ucunu aç
      (`MapMcp("/mcp").RequireAuthorization()` + T008 extension çağrısı) —
      `src/services/{basket/Basket.Api,order/Order.Api,customer/Customer.Api,payment/Payment.Api}/Program.cs`
      (storefront/catalog/stock DOKUNULMAZ — R3)
- [X] T011 [US1] `place_order` zincirinin scope policy'sini teyit et (OrderWrite mi CheckoutWrite
      mi); gerekirse demeti güncelle — `src/services/order/Order.Api` + `ExternalAgentDefaults.cs`
- [X] T012 [US1] CANLI: quickstart §1 curl zinciri PASS (401 challenge, metadata, discovery,
      storefront anonim kaldı)
- [ ] T013 [US1] CANLI: Claude Code bağlan (`claude mcp add` ×4) → tarayıcı login + consent →
      authenticated; ardından E2E alışveriş: ara→sepete at→sipariş ver→siparişleri gör
      (quickstart §2–3, FR-010)

**Checkpoint**: MVP canlı — ekransız alışveriş kanıtlandı.

---

## Phase 4: User Story 2 — Yeni kullanıcı kayıtla girer (P2)

**Goal**: Bağlantı seremonisi içinde register → customer rolü → kesintisiz consent → alışveriş.

**Independent Test**: quickstart §2 register varyantı.

- [X] T014 [US2] OAuth authorize akışından gelen kullanıcı için login sayfasındaki Register
      yolunun returnUrl'ü koruduğunu doğrula; kopukluk varsa düzelt —
      `src/others/Identity.Server/` (login/register sayfaları)
- [ ] T015 [US2] CANLI: temiz kullanıcıyla bağlan → register → consent → ilk alışveriş
      (customer rolü otomatik; US1 zinciri yeni hesapla PASS)

**Checkpoint**: Yeni kullanıcı kanala tek seremoniyle giriyor.

---

## Phase 5: User Story 3 — Sessiz yenileme + mevcut kanallar (P3)

**Goal**: İkinci oturum ekransız; revoke çalışır; hiçbir mevcut kanal kırılmadı.

**Independent Test**: quickstart §4–5.

- [ ] T016 [US3] CANLI: kısa access-token ömrüyle sessiz refresh doğrula (süre dolumu →
      ekransız yeni token → tool çağrısı PASS); revocation ucuyla iptal → 401 → yeniden bağlanma
      (quickstart §4)
- [ ] T017 [US3] CANLI REGRESYON: WebApp login→sepet→checkout; ChatAgent PUBLIC arama +
      ASSISTANT sipariş; UserKey'li MCP curl; `dotnet test` yeşil (quickstart §5, SC-004)

**Checkpoint**: Tüm hikâyeler bağımsız doğrulandı.

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 [P] CLAUDE.md güncelle: identity-server satırına DCR+consent+revocation notu;
      "Projeye özel yetki" bölümüne dış-agent MCP OAuth özeti — `CLAUDE.md`
- [X] T019 [P] Guard'lar + tam derleme: `scripts/check-claude-spec-links.sh`,
      `scripts/check-flow-links.sh`, `dotnet build`, `dotnet test` (FLOW.md tetiklenmedi — İlke VII
      dar tetik, domain süreci değişmedi)
- [ ] T020 Commit + PR: quickstart çıktı özetiyle (canlı PASS kanıtları PR gövdesine)

---

## Dependencies & Execution Order

- **Phase 2**: T002→T005 (demet, validator); T003→T004→T005 (test-first zinciri);
  T006/T007/T008/T009 birbirinden bağımsız [P].
- **US1 (Phase 3)**: T010 T008'e bağlı; T012 T009+T010'a; T013 T005+T006+T007+T012'ye bağlı.
- **US2 (Phase 4)**: Foundational + T013'ün seremoni yolunu kullanır; kod işi (T014) bağımsız
  başlayabilir.
- **US3 (Phase 5)**: T016 T013'e bağlı; T017 her şeyin sonunda.
- **Sıralı MVP önerisi**: T001→T002..T009→T010..T013 (MVP) → T014..T015 → T016..T017 → T018..T020.

## Parallel Opportunities

- Phase 2: T002+T003 birlikte; sonra T006+T007+T008+T009 dört koldan.
- Phase 6: T018+T019 paralel.

## Implementation Strategy

MVP = Phase 1–3 (T001–T013): keşif zinciri + DCR + consent + 4 servis koruması + canlı E2E.
US2/US3 doğrulama ağırlıklı küçük fazlar. Her checkpoint'te durup canlı doğrulama; T013 PASS
olmadan Phase 4'e geçilmez.