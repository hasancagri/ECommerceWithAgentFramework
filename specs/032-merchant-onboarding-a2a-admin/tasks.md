# Tasks: Admin Metinle Onboarding (back-end MCP routing)

**Feature**: `032-merchant-onboarding-a2a-admin` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

**Girdi**: plan.md, spec.md (US1 register / US2 status), data-model.md, contracts/, research.md, quickstart.md

**Not**: Yeni proje YOK. WebApp'e onboarding MCP yüzeyi + admin ekran + BFF admin kol; ChatAgent'a `admin`
persona + prompt. Testler: E2E (Playwright, feature-sonrası); saf domain birim testi yok (yeni domain yok).

**Yollar**: `src/ui/WebApp/...` (WebApp) ve `src/agents/ChatAgent/...` (ChatAgent) köküne göreli.

---

## Phase 1: Setup

- [ ] T001 Baseline: `dotnet build` (WebApp + ChatAgent) yeşil mi doğrula; değilse nedeni not et.
- [ ] T002 [P] Config: WebApp onboarding MCP server url'i ChatAgent config'ine (appsettings) ekle —
  `src/agents/ChatAgent/appsettings.json` (diğer McpServers url deseni / Aspire service discovery).
- [ ] T003 [P] `src/ui/WebApp/appsettings.json`: `DropShopGateway` bölümündeki `McpUrl`'in dinamik gateway
  MCP adresine işaret ettiğini teyit et (dev; Options `DropShopGatewayOption` 032-prep'te bağlı).

---

## Phase 2: Foundational (US1 + US2 için bloklayan ortak altyapı)

- [ ] T004 `src/ui/WebApp/GatewayOnboarding/OnboardingMcpTools.cs`: MCP tool yüzeyi iskeletini oluştur
  (`[McpServerToolType]`), `GatewayRegistrationClient` enjekte; tool gövdeleri US1/US2'de doldurulur.
- [ ] T005 `src/ui/WebApp/Program.cs`: MCP server host'u kaydet (`AddMcpServer().WithToolsFromAssembly()`
  + `MapMcp(...)`), yüzeyi **admin** ile koru (rol-cookie / eşdeğer). GatewayRegistrationClient + IChallengeStore
  kayıtları mevcut (değişmez).
- [ ] T006 `src/agents/ChatAgent/ConstValues.cs`: `McpServers.Onboarding` (+ allowedTools:
  `submit_registration`, `registration_status`) sabiti ekle; `Prompts.AdminOnboardingInstructions` prompt'unu
  yaz (contracts §3: register/status niyet→tool, sonucu metinle, shopper kapsam dışı, graceful mesaj).
- [ ] T007 `src/agents/ChatAgent/Program.cs`: `adminAgentTools` demeti (yalnız onboarding MCP) + WebApp
  onboarding MCP named HttpClient/registry + `AddAIAgent("admin", ... CollectTools(adminAgentTools),
  Prompts.AdminOnboardingInstructions)`. URL yok/erişilemezse graceful-degrade (tool eklenmez), boot çökmez.
- [ ] T008 `src/ui/WebApp/Pages/Admin/Onboarding.cshtml` + `.cshtml.cs`: admin-korumalı metin (chat) ekranı
  (`@attribute [Authorize(Roles = "admin")]`), mevcut chat widget desenini kullan.
- [ ] T009 `src/ui/WebApp/Chat/ChatEndpoints.cs`: admin kolu — admin rolünde `admin` persona'ya proxy
  (`/admin/v1/responses`) veya ayrı `/chat/admin/stream`; rol-korumalı; SSE pass-through (şema değişmez).

**Checkpoint (Foundational)**: `dotnet build` 0 hata; admin ekranı açılır (tool boş olsa da); anonim/normal
erişemez; `admin` persona kayıtlı, onboarding MCP client bağlı (URL varsa).

---

## Phase 3: US1 — Admin metinle başvuru (Priority: P1) 🎯 MVP

**Story hedefi**: Admin doğal dille "X'i kaydet" → `submit_registration` MCP tool'u → WebApp içinde
`RegisterAsync` iki-adım+challenge → Pending, metin yanıt.

**Independent Test**: Admin ekranına "shop.example.com'u kaydet" → başvuru Pending, yanıt metinle döner;
challenge iki-adımı WebApp'te çözülür; gateway'de RegisterRequest oluşur. (quickstart S1)

- [ ] T010 [US1] `src/ui/WebApp/GatewayOnboarding/OnboardingMcpTools.cs`: `submit_registration` tool'unu
  doldur — girdi `descriptorUrl?` (yoksa kendi well-known origin'i), `GatewayRegistrationClient.RegisterAsync`
  çağırır, sonucu (`status`, `requestId?`, `message?`) döner. (contracts §1)
- [ ] T011 [US1] `src/agents/ChatAgent/Program.cs` / `ConstValues.cs`: `submit_registration`'ın `admin`
  persona allowedTools'unda olduğunu doğrula; prompt'ta register niyet→tool eşlemesi net.
- [ ] T012 [US1] Doğrula (quickstart S1): admin ekranından başvuru → Pending metin; gateway RegisterRequest;
  challenge WebApp'te (S5 coexist da bozulmadı).

**Checkpoint US1**: MVP — admin metinle başvuru uçtan uca çalışır.

---

## Phase 4: US2 — Admin durum sorgusu (Priority: P2)

**Story hedefi**: Admin "durumu ne?" → `registration_status` MCP tool → `StatusAsync(domain)` → gateway
registration_status → durum + Message metni.

**Independent Test**: Var olan domain için "durumu ne?" → durum + Message metni döner. (quickstart S2)

- [ ] T013 [US2] `src/ui/WebApp/GatewayOnboarding/GatewayRegistrationClient.cs`: `StatusAsync(string domain)`
  ekle — mevcut `CreateMcpClientAsync`/token deseniyle gateway `registration_status` MCP tool'unu çağırır,
  `{ status, requestId?, message }` döner. `RegisterAsync` değişmez.
- [ ] T014 [US2] `src/ui/WebApp/GatewayOnboarding/OnboardingMcpTools.cs`: `registration_status` tool'unu
  doldur — girdi `domain`, `StatusAsync` çağırır, sonucu döner. (contracts §1)
- [ ] T015 [US2] `src/agents/ChatAgent/...`: `registration_status`'ın `admin` persona allowedTools'unda +
  prompt'ta status niyet→tool eşlemesi net.
- [ ] T016 [US2] Doğrula (quickstart S2): "durumu ne?" → güncel durum + Message metni.

**Checkpoint US2**: Başvuru + durum ikisi de metinle çalışır.

---

## Phase 5: Polish & Cross-Cutting

- [ ] T017 [P] Persona izolasyonu (quickstart S0b/S6): onboarding tool'ları `public`/`assistant` persona
  setlerinde YOK; shopper "kaydet" derse yapamaz. Doğrula.
- [ ] T018 [P] Yetki (quickstart S3): normal/anonim kullanıcı `/Admin/Onboarding` + admin BFF ucu + WebApp
  MCP yüzeyine erişemez (403/görünmez). Doğrula.
- [ ] T019 [P] Graceful-degrade (quickstart S4): onboarding MCP url yok/gateway erişilemez → ekran açılır,
  "kullanılamıyor" der, boot çökmez, diğer chat çalışır. Doğrula.
- [ ] T020 Config Options (SC-006): WebApp MCP url + gateway bağlantısı Options POCO ile okunur, magic-string
  `config[...]` yok. Doğrula.
- [ ] T021 `dotnet build` (WebApp + ChatAgent) 0 hata; (opsiyonel) E2E (Playwright) admin onboarding akışı +
  yetkisiz erişim reddi harness'a eklenir (feature-sonrası; LLM tool-seçimi E2E dışı).

---

## Dependencies

- **Setup (T001–T003)** → önce.
- **Foundational (T004–T009)** → US1/US2'yi BLOKLAR (MCP yüzeyi + admin persona + ekran + proxy ortak).
- **US1 (T010–T012)** → Foundational sonrası; MVP.
- **US2 (T013–T016)** → Foundational sonrası; US1'den bağımsız (farklı tool + StatusAsync). US1 ile paralel olabilir.
- **Polish (T017–T021)** → tüm story'ler sonrası.

## Parallel opportunities

- Setup: T002 (ChatAgent config) ‖ T003 (WebApp config).
- Foundational: WebApp kolu (T004/T005/T008/T009) ‖ ChatAgent kolu (T006/T007) büyük ölçüde paralel.
- US1 ‖ US2: farklı tool'lar (submit vs status) + StatusAsync ayrı — Foundational sonrası paralel yürüyebilir.
- Polish: T017/T018/T019 [P] bağımsız doğrulamalar.

## Implementation strategy

- **MVP = Foundational + US1** (metinle başvuru): en büyük değer; tek başına teslim/test edilebilir.
- **Artımlı**: US2 (durum) eklenir. Her checkpoint'te build yeşil.
- **Coexist**: `POST /gateway-onboarding/register` + `GatewayRegistrationClient.RegisterAsync` dokunulmaz;
  MCP tool onu sarar. Gateway tarafı değişmez.