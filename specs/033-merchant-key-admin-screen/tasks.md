---
description: "Task list — Admin MerchantKey Ekranı (033)"
---

# Tasks: Admin MerchantKey Ekranı

**Input**: `specs/033-merchant-key-admin-screen/spec.md`

**Prerequisites**: spec.md (plan atlandı — küçük feature, artefakt ölçekleme)

**Tests**: Yeni saf domain mantığı yok (aggregate davranışı 886563b'de testli; yeni slice read-only).
Domain-TDD kapsamına giren test çıkmaz; doğrulama canlı (Aspire).

**Organization**: Görevler user story'ye göre gruplu; her story bağımsız test edilebilir.

**Kapsam**: `src/services/customer/Customer.Api` (yalnız yeni query slice) + `src/ui/WebApp`.
Yeni tablo/aggregate/event/scope yok. Yazma mevcut `SetMerchantInformation` ucuyla.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: User Story 1 — Merchant kimliğini kaydet (P1)

**Goal**: Admin, merchantId + MerchantKey'i ekrandan girer; değer customerDb'ye kalıcı yazılır.

**Independent Test**: Onaylı kimlikle form gönder → başarı mesajı; Customer.Api tablosunda kayıt.

- [x] T001 [US1] `src/ui/WebApp/Pages/Admin/Dto/MerchantInformationDtos.cs` ekle:
  `SetMerchantInformationRequest(Guid MerchantId, string MerchantKey)`.
- [x] T002 [US1] `src/ui/WebApp/Services/Refit/ICustomerRefitService.cs`'e
  `[Post("/api/v1/merchant-information")] SetMerchantInformationAsync(SetMerchantInformationRequest)` ekle.
- [x] T003 [US1] `src/ui/WebApp/Services/MerchantInformationService.cs` oluştur (CustomerService deseni):
  `SetAsync` — key'i trim'ler, hatada `ServiceResult.Error` + `LogProblemDetails`.
- [x] T004 [US1] `src/ui/WebApp/Pages/Admin/Onboarding.cshtml.cs`: `[BindProperty]` form modeli
  (MerchantId, MerchantKey) + `OnPostSaveMerchantKeyAsync` handler; boş alan → sayfada hata mesajı.
- [x] T005 [US1] `src/ui/WebApp/Pages/Admin/Onboarding.cshtml`: "Merchant Kimliği" bölümü — merchantId
  (text) + MerchantKey (`type="password"`) form + başarı/hata mesaj alanı. Key hiçbir yerde geri basılmaz.

**Checkpoint**: US1 tek başına MVP — kayıt sonrası vault tokenize çalışır (FR-001/002/004/005/008).

---

## Phase 2: User Story 2 — Mevcut kaydı gör (P2)

**Goal**: Sayfa açılışında kayıt durumu görünür: yok / merchantId + son güncelleme zamanı. Key asla.

**Independent Test**: Kayıt varken sayfayı aç → merchantId + zaman görünür, key alanı boş.

- [x] T006 [US2] `src/services/customer/Customer.Api/Domains/MerchantInformations/Features/Queries/GetMerchantInformation.cs`
  oluştur: Query + `Response{MerchantId, UpdatedTime}` + Handler (`IDocumentSession` okuma; kayıt yok → NotFound).
- [x] T007 [US2] Aynı dosyada endpoint-extension: `MapGet("")` +
  `.RequireAuthorization(AuthorizationScopes.MerchantCredentialsWrite)`;
  `MerchantInformationEndpointExtension.cs`'e zincirle. Response'ta key alanı YOK.
- [x] T008 [P] [US2] `MerchantInformationDtos.cs`'e `MerchantInformationStatusDto(Guid MerchantId, DateTime? UpdatedTime)`;
  `ICustomerRefitService`'e `[Get("/api/v1/merchant-information")]` metodu ekle.
- [x] T009 [US2] `MerchantInformationService.GetAsync`: NotFound → başarılı-boş (kayıt yok durumu; hata değil).
- [x] T010 [US2] `Onboarding.cshtml.cs` `OnGet`'te durumu yükle; `Onboarding.cshtml` durum bloğu:
  "kayıtlı merchant kimliği yok" ya da merchantId + güncelleme zamanı (FR-003).

**Checkpoint**: Kayıt teyidi ekrandan; SC-003 (key görünmez) korunur.

---

## Phase 3: User Story 3 — Yetki sınırı (P3)

**Goal**: Ekran + uçlar yalnız admin. Yeni kod yetki eklemez, mevcut guard'lar doğrulanır.

**Independent Test**: customer token'ı + anonim ile ekran ve uçlara istek → hepsi RET.

- [ ] T011 [US3] Canlı doğrulama: customer rolüyle `/Admin/Onboarding` → redirect/403; customer token'ıyla
  GET+POST `customer/api/v1/merchant-information` → 403; anonim → 401 (FR-006, SC-005).

---

## Phase 4: Polish & Doğrulama

- [x] T012 [P] Ölü kod sil: `src/ui/WebApp/GatewayOnboarding/MerchantCredentialStore.cs` (interface+impl),
  `GatewayOnboardingEndpoints.cs`'ten `/gateway-onboarding/merchant-key` ucu + `SetMerchantKeyRequest`,
  `Program.cs:175` DI satırı (FR-007).
- [ ] T013 `dotnet build` temiz; Aspire canlı uçtan uca: key kaydet → kart ekle → vault tokenize OK (SC-002);
  restart sonrası durum korunur (SC-004). Bu koşum 032 T016/T017 + PG-017 T014 canlılarını da işaretlemeye aday.
- [x] T014 (kapsam eki, anayasa v1.8.1) WebApp'ten imperatif MCP kaldırıldı: `GatewayRegistrationClient` +
  `/gateway-onboarding/register` + `DropShopGatewayOption` + `ModelContextProtocol.Core` paketi silindi.
- [x] T015 (kapsam eki) Tüketicisiz merchant-descriptor yüzeyi silindi: `GatewayOnboarding/` klasörü,
  `Options/GatewayOnboarding`, OptionsExt kaydı, appsettings section'ı (016'dan beri gateway okumuyordu).

---

## Dependencies & Sıra

- US1 (T001→T005 sıralı) → US2 (T006/T007 backend, T008 [P] paralel; T009→T010) → US3 → Polish.
- T012 bağımsız dosyalar, her an yapılabilir; T013 en son.

## MVP

**US1** tek başına MVP: key tabloya girer, vault çalışır. US2 teyit ekranı, US3 guard doğrulaması.