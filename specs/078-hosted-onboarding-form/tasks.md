# Tasks: Hosted Merchant Onboarding + Ekrandan Credential Teslimi

**Input**: Design documents from `/specs/078-hosted-onboarding-form/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/pg-onboarding-rest.md

**Tests**: İLKE VI — saf domain (CredentialEntrySession + MerchantInformation davranış değişikliği)
test-first ZORUNLU; handler/endpoint/HTML testleri yazılmaz (canlı doğrulama quickstart.md ile).

**Organization**: User story bazlı; US2 PG repo'sunda (burada yalnız kabul doğrulaması).

**NOT — PG bağımlılığı**: US1/US3/US4 canlı doğrulamaları PG'nin
[contracts/pg-onboarding-rest.md](contracts/pg-onboarding-rest.md)'i implemente etmesini bekler
(ayrı repo çalışması). Store kodu PG'siz yazılıp build/test edilebilir; canlı doğrulama task'ları
PG hazır olana dek bekler.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 `DropShopOnboardingOption`'ı REST'e çevir (`McpUrl` → `ApiBaseUrl`) —
  `src/services/customer/Customer.Api/Onboarding/DropShopOnboardingOption.cs`; YENİ
  `CredentialEntryOptions` POCO'su (`PublicBaseUrl` = Customer.Api'nin dışarıdan erişilir adresi +
  `LinkLifetime` varsayılan 60 dk) — `src/services/customer/Customer.Api/Options/CredentialEntryOptions.cs`;
  `appsettings.Development.json`'a iki section (`ApiBaseUrl: http://localhost:5202`,
  `PublicBaseUrl: Customer.Api dev URL'i` — launchSettings'ten bak). Eski MCP istemcisi US4 sökümüne
  dek geçici `ApiBaseUrl + "/mcp"` ile derlenir.

## Phase 2: Foundational (Blocking)

- [X] T002 `PgOnboardingClient` tipli REST istemcisi (D3): `CreateSessionAsync(email)`,
  `GetStatusAsync(email)`, `ValidateCredentialsAsync(merchantId, merchantKey)`; ulaşım hatasında null
  (dostane hata deseni) — `src/services/customer/Customer.Api/Onboarding/PgOnboardingClient.cs`;
  kayıt `AddHttpClient<PgOnboardingClient>().AddHttpMessageHandler<OnboardingGatewayTokenHandler>()`
  (mevcut extension/Program.cs kablosuna ekle).

**Checkpoint**: REST istemci hazır — US1 ve US3 paralel başlayabilir.

## Phase 3: User Story 1 — PII'siz başvuru başlatma (P1) 🎯 MVP

**Goal**: Agent tek çağrıyla PG hosted form linki döndürür; PII sohbete girmez.

**Independent Test**: quickstart S1 — linkten form doldurulur, PG'de Pending doğar, transkriptte PII yok.

- [X] T003 [US1] `AdminStartOnboarding` agent slice + MCP tool: e-posta alır,
  `PgOnboardingClient.CreateSessionAsync` çağırır; `formUrl` + durum mesajı döner; Pending-varken
  formUrl=null mesajı; `[RequiredScope(MerchantCredentialsWrite)]` —
  `src/services/customer/Customer.Api/Domains/MerchantInformations/Features/Agents/Commands/AdminStartOnboarding.cs`
- [X] T004 [US1] Tool adını kaydet (D7 tuzağı): `Shared.CustomerAdminTools`'a `StartOnboarding` sabiti
  (`src/others/Shared/McpToolNames.cs`) + `customerAdminToolNames` allowlist'ine ekle
  (`src/services/customer/Customer.Api/Program.cs`).
- [ ] T005 [US1] Canlı doğrulama S1 (quickstart.md) — PG kontrat implementasyonu hazır olunca.

**Checkpoint**: US1 tek başına gösterilebilir (PG hazırsa).

## Phase 4: User Story 2 — Tek kullanımlık credential teslimi (P1, PG tarafı)

**Goal**: Approve → mail + tek kullanımlık teslim linki (PG sitesinde bir kez gösterim).

**Independent Test**: quickstart S2 — Mailpit'te mail, ilk açılışta ikili, ikincide yok.

- [ ] T006 [US2] PG repo çalışması kontrata göre yapılır (bu repo'da kod YOK); kabul doğrulaması
  S2 quickstart senaryosuyla — referans `specs/078-hosted-onboarding-form/contracts/pg-onboarding-rest.md`.

## Phase 5: User Story 3 — Store ekranından credential girişi (P1)

**Goal**: Agent yalnız ekran linki verir; MerchantId+Key ekrana girilir, kayıt anında PG'ye doğrulanır.

**Independent Test**: quickstart S3 — link tek kullanımlık; yanlış key anında hata; ödeme akışı yeni
key ile çalışır; izde key yok.

- [X] T007 [P] [US3] Domain testleri ÖNCE (İLKE VI, red): `CredentialEntrySession.Create/Consume/IsUsable`
  (süre dolumu, çift tüketim, mutlu yol) + `MerchantInformation` credential-set davranışının
  `CredentialsVerified` bayrağı — `tests/Customer.Api.Tests/CredentialEntrySessionTests.cs`
- [X] T008 [US3] `CredentialEntrySession` aggregate (green; data-model.md şeması: 256-bit URL-safe
  Token, ExpiresAt, ConsumedAt, ResultDomain dönen Consume) —
  `src/services/customer/Customer.Api/Domains/MerchantInformations/CredentialEntrySession.cs`
- [X] T009 [US3] `MerchantInformation`'a `CredentialsVerified` alanı + credential-set davranış
  güncellemesi (green) —
  `src/services/customer/Customer.Api/Domains/MerchantInformations/MerchantInformation.cs`
- [X] T010 [US3] `AdminRequestCredentialEntryLink` agent slice + MCP tool: session dokümanı yazar,
  `{CredentialEntryOptions.PublicBaseUrl}/merchant-credentials/{token}` linki döner (HttpContext base
  KULLANILMAZ — MCP çağrısı mcp-gateway proxy'sinden gelir, istek base'i Aspire iç adresidir,
  tarayıcıda çözülmez),
  `AdminActionLog: credential_entry_link_created` (token loglanmaz, oturum Id loglanır);
  `[RequiredScope(MerchantCredentialsWrite)]` —
  `src/services/customer/Customer.Api/Domains/MerchantInformations/Features/Agents/Commands/AdminRequestCredentialEntryLink.cs`
- [X] T011 [US3] Tool adı kaydı (D7): `CustomerAdminTools.RequestCredentialEntryLink` sabiti +
  `customerAdminToolNames` allowlist — `src/others/Shared/McpToolNames.cs`,
  `src/services/customer/Customer.Api/Program.cs`
- [X] T012 [US3] `SubmitMerchantCredentials` command slice: token'lı session yükle → `Consume` →
  `PgOnboardingClient.ValidateCredentialsAsync` (geçersiz=ret; PG-yok=`CredentialsVerified:false` ile
  kaydet) → `MerchantInformation` güncelle → `AdminActionLog: merchant_credentials_submitted`
  (key YAZILMAZ) —
  `src/services/customer/Customer.Api/Domains/MerchantInformations/Features/Commands/SubmitMerchantCredentials.cs`
- [X] T013 [US3] `CredentialEntryEndpointExtension`: `GET /merchant-credentials/{token}` (gömülü HTML
  form, yazma-only, usable-değilse nötr 404) + `POST /merchant-credentials/{token}` (form-post →
  IMessageBus → sonuç sayfası); anonim (token=yetki, SAPMA-1) + Program.cs map —
  `src/services/customer/Customer.Api/CredentialEntryEndpointExtension.cs`
- [ ] T014 [US3] Canlı doğrulama S3 (quickstart.md) — negatifler dahil (yanlış key / ikinci kullanım /
  süre) + ödeme akışı smoke.

**Checkpoint**: Yeni akış uçtan uca canlı — söküm serbest.

## Phase 6: User Story 4 — PII'li sohbet yüzeyinin sökümü (P2; S1-S3 CANLI PASS SONRASI)

**Goal**: `/mcp-admin`'de PII isteyen ya da MerchantKey döndüren yüzey kalmaz.

**Independent Test**: quickstart S4 — tool listesi + status yanıtı denetimi.

- [X] T015 [US4] `AdminOnboardingStatus`'u yeniden şekillendir: `PgOnboardingClient.GetStatusAsync`'e
  geç; yanıttan `MerchantId`/`MerchantKey` alanlarını çıkar; Approved mesajı mail+ekran yoluna
  yönlendirir —
  `src/services/customer/Customer.Api/Domains/MerchantInformations/Features/Agents/Queries/AdminOnboardingStatus.cs`
- [X] T016 [US4] Söküm: `AdminSubmitOnboarding.cs` + `AdminSetMerchantCredentials.cs` +
  `Onboarding/MerchantOnboardingClient.cs` + `OnboardingResultParser` sil; `CustomerAdminTools`'tan
  eski sabitler + `customerAdminToolNames`'ten eski adlar düşür; T001 geçici `McpUrl` türevi kalksın —
  ilgili dosyalar + `src/others/Shared/McpToolNames.cs` + `src/services/customer/Customer.Api/Program.cs`
- [X] T017 [US4] Canlı doğrulama S4 (quickstart.md). — KAPANIŞ: kullanıcı kararıyla canlı tur atlandı; kod-denetimi + allowlist/sabit sökümü kanıt sayıldı (2026-09-19).

## Phase 7: Polish & Cross-Cutting

- [X] T018 [P] Customer `FLOW.md` güncelle (İLKE VII, aynı PR): onboarding süreci yeni adımlarla
  (link üret → PG formu → approve-mail → teslim linki → store ekranı → doğrula-kaydet); eski chat-PII
  adımları sil — `src/services/customer/FLOW.md`; `scripts/check-flow-links.sh` yeşil.
- [X] T019 [P] `CLAUDE.md` customer BC satırı + 070 sapma notu güncelle (imperatif MCP sapması
  SÖKÜLDÜ; hosted credential ekranı SAPMA-2 olarak not) — `CLAUDE.md`.
- [X] T020 Tam doğrulama: (build+test+guard yeşil; S5 canlı PASS; sandbox key rotate KULLANICIDA bekler) `dotnet build` + `dotnet test` (bağımlı TEST projeleri dahil — rename
  tuzağı) + quickstart S5 (PG kapalı dostane hata) + sandbox MerchantKey rotate hatırlatması.

## Dependencies & Execution Order

- Phase 1 → Phase 2 → (US1 ∥ US3) → US4 → Polish. US2 = PG repo'su, US1'den bağımsız yürür;
  S2/S3 canlı doğrulamaları PG işini bekler.
- US4 KESİN sıra kuralı: T015-T017 ancak T005+T014 (canlı PASS) sonrası (FR-009).
- T007 (test, red) T008-T009'dan ÖNCE; T008-T009 → T010/T012; T012 → T013.
- T003/T004 ile T007-T013 farklı dosyalar — iki story paralel yürüyebilir.

## Parallel Example: US1 + US3 başlangıcı (Phase 2 sonrası)

```text
Paralel: T003 (AdminStartOnboarding) ∥ T007 (CredentialEntrySessionTests, red)
Sonra:   T004 ∥ T008 → T009 → T010 → T012 → T013
```

## Implementation Strategy

- **MVP = US1**: T001→T005; PG form hazır değilse bile store kodu biter, S1 beklemede kalır.
- **Sonra US3** (ekran zinciri) → S3 canlı PASS → **US4 söküm** → Polish.
- Her task/mantıksal grup sonrası commit; checkpoint'lerde durup canlı doğrulama.