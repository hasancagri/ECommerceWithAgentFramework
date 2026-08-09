# Tasks: Admin Metinle Onboarding (back-end MCP routing)

**Feature**: `032-merchant-onboarding-a2a-admin` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

**Girdi**: plan.md, spec.md (US1 register / US2 status), data-model.md, contracts/, research.md, quickstart.md

## Tasarım revizyonu (implement sırasında, kullanıcı kararı)

- **WebApp'te MCP YOK.** Onboarding MCP yüzeyi (D4 challenge-locality) iptal. WebApp yalnız admin
  ekran + BFF proxy kolu barındırır; agent hedef servisin MCP'sine gider.
- **Metin akışında MCP DOLAYLI:** ChatAgent `admin` persona DropShop Merchant.Api `/mcp`'yi toplar
  (`submit_registration`, `registration_status`); LLM prompt ile çağırır (elle `CallToolAsync` yok).
  Kural CLAUDE.md'ye işlendi.
- **Challenge KALDIRILDI** (kullanıcı kararı): domain-control challenge fazlalık — admin descriptor'ı
  (legalName/taxId/contactEmail) insan olarak inceler. DropShop'ta challenge tümüyle silindi; `submit`
  descriptor doğrulanınca doğrudan **Pending** doğar, insan `Approve`/`Reject` eder. (Ayrı repo/branch.)
- Sonuç: research D3 korunur (Merchant.Agent A2A kullanılmaz); **D4 geçersiz** (challenge yok);
  gateway'e makine token'ıyla gidilir; render mevcut SSE chat ile aynı.

---

## Phase 1: Setup

- [X] T001 Baseline: `dotnet build` (WebApp + ChatAgent) yeşil.
- [X] T002 ChatAgent config: `DropShopGateway` + `Onboarding` bölümleri `appsettings.json`'a eklendi
  (strongly-typed POCO; magic-string yok).
- [X] T003 AppHost: `chat-agent`'a `ecommerce-web` referansı (descriptor URL service discovery).

---

## Phase 2: DropShop — challenge kaldırma (ayrı repo, `remove-onboarding-challenge` branch)

- [X] T004 `RegisterRequest`: `AwaitingDomainControl` + challenge alanları/metotları (`IssueChallenge`,
  `VerifyChallenge`, `ChallengeOutcome`) silindi; `CreateAwaiting` → `CreatePending` (doğrudan Pending).
- [X] T005 `SubmitRegistrationForAgent`: challenge çek/doğrula adımları çıktı; descriptor doğrula →
  Pending + admin mail. `ChallengeRequired`/Token/ExpectedValue yanıtı kalktı.
- [X] T006 `RegistrationStatusForAgent` + `GetRegisterRequest`/`GetRegisterRequests`: AwaitingDomainControl
  mesajı + `ChallengeResult` alanları çıktı. MCP tool açıklamaları güncellendi.
- [X] T007 Admin UI (`ApiModels` + `RegisterRequests/Index.cshtml`) + `Merchant.Agent` prompt/card +
  `Program.cs` yorumu challenge'tan arındı.
- [X] T008 `RegisterRequestTests` challenge testleri silindi; CreatePending/Approve/Reject/descriptor
  kaldı. `dotnet build` 0 hata; `dotnet test` 68 PASS.

---

## Phase 3: ChatAgent — admin persona (US1 + US2)

- [X] T009 `Options/DropShopGatewayOption.cs` + `Options/OnboardingOption.cs` (IdentityServerSettings
  deseni: düz POCO + computed `TokenEndpoint`). `GetSection().Get<T>()` + `AddSingleton`.
- [X] T010 `OnboardingGatewayTokenHandler.cs`: client_credentials makine token (cache −30 sn); her
  isteğe Bearer takar (keşif ListTools dahil). DropShop dev cert muaf.
- [X] T011 `ConstValues.cs`: `McpServers.MerchantOnboarding`, `McpClients.MachineOnboarding`,
  `OnboardingTools` (submit_registration + registration_status), `Prompts.AdminOnboardingInstructions`.
- [X] T012 `Program.cs`: `adminAgentTools` (yalnız onboarding MCP) + `MachineOnboarding` named-client
  (resilience muaf + token handler) + `AddAIAgent("admin", ...)` (descriptor/domain prompt'a eklenir) +
  `MapOpenAIResponses(admin, "/admin/v1/responses")`. Config yok → tool'suz açılır (graceful-degrade).

---

## Phase 4: WebApp — admin ekran + BFF kolu (MCP YOK)

- [X] T013 `Chat/ChatEndpoints.cs`: `/chat/admin/stream` — `[Authorize] Roles=admin` (cookie); `admin`
  persona'ya (`/admin/v1/responses`) proxy, user token forward, SSE pass-through.
- [X] T014 `Pages/Admin/Onboarding.cshtml(.cs)`: `[Authorize(Roles="admin")]` metin (chat) ekranı;
  inline SSE script `/chat/admin/stream`'e gider.
- [X] T015 `_Layout.cshtml`: admin menüsüne "Merchant Onboarding" linki (IsInRole admin).

---

## Phase 5: Doğrulama (canlı — Aspire; feature-sonrası)

- [ ] T016 [US1] S1: admin ekranından "mağazayı gateway'e kaydet" → Pending metin; DropShop'ta
  RegisterRequest Pending (challenge yok).
- [ ] T017 [US2] S2: "başvurum ne durumda?" → registration_status → durum + Message.
- [ ] T018 S3 yetki: normal/anonim `/Admin/Onboarding` + `/chat/admin/stream` → reddedilir (403/redirect).
- [ ] T019 S4 graceful-degrade: DropShop erişilemez → admin ekranı açılır, "onboarding kullanılamıyor";
  ChatAgent boot çökmez; diğer chat çalışır.
- [ ] T020 S6 persona izolasyonu: assistant/public'te onboarding tool YOK; shopper "kaydet" derse yapamaz.

**Not**: T016-T020 canlı (Aspire + DropShop ayakta). LLM tool-seçimi E2E dışı — elle/quickstart.

---

## Build durumu

- ECommerce: WebApp + ChatAgent + AppHost `dotnet build` 0 hata.
- DropShop: `dotnet build` 0 hata; Merchant.Api.Tests 68 PASS.