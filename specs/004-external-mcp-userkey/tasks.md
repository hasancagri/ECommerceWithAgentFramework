---
description: "Task list for External MCP UserKey"
---

# Tasks: External MCP UserKey

**Input**: Design documents from `/specs/004-external-mcp-userkey/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Anayasa kalite-gate'i gereği yeni davranış (hash/üretim/iptal/scope) için hedefli
**saf birim testleri** dahil edildi (xUnit + Shouldly; host harness yok).

**Organization**: Görevler user story'lere göre gruplu; her story bağımsız test edilebilir artımdır.

## Implementation Status (as-built, 2026-07-21)

**Yapıldı (tüm çözüm derleniyor 0 hata, 9/9 birim testi geçti):** Foundational (T003–T012),
US1 çekirdek (T013–T015 — **8 servisin hepsi wired**), US2 uçları (T018–T019), US4 kayıt ekranı
scope seçimi + UserScopes yazımı (T022–T023), US3 kod-doğrulaması (T026), amendment (T031).
**CANLI DOĞRULANDI (2026-07-21, taze AppHost + yeni kod):** US4 kayıt→UserScopes(basket.write);
US2 issue+revoke (revoke anında etkili); US1 resolve(200+scopes) + gateway MCP add_to_cart
(isSuccess, doğru user'ın sepeti) + bad/no key→red (T017/T020/T021/T025 ✅).
**Ek (2026-07-21):** T016 handler birim testi (Common.Tests, 3/3); T026 DÜZELTİLDİ (6 Agent read
slice'ından read-scope kaldırıldı → read'ler gerçekten anonim, canlı doğrulandı T027). Testler: 12/12.
**Kalan (küçük):** süresizlik senaryosu (T030), US4 page-model birim testi (T024 — host harness
gerektirir, anayasa kaçınır; canlı doğrulandı).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1..US4 (spec.md); Setup/Foundational/Polish etiketsiz

## Path notu

Değişiklikler iki yerde: `src/others/Identity.Server/` (kalıcılık + uçlar + kayıt ekranı) ve
`src/others/Common/` (paylaşılan custom auth). Servis Program.cs'leri yalnızca şemayı devreye alır.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Konfigürasyon ve scope tanımları

- [x] T001 Config.cs'e `apikeys.manage` ApiScope + operatör-sunulan scope listesi sabiti ekle; `apikeys.manage`'i bir admin client'a (veya bff'e) grant et — issue/revoke çağrılabilsin (src/others/Identity.Server/Config.cs)
- [x] T002 [P] Resolve iç-secret + header adı ("X-User-Key"/"X-Internal-Secret") config anahtarlarını ekle (Identity.Server appsettings.*.json + Common option)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm story'lerin dayandığı entity + auth altyapısı

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz

- [x] T003 [P] `ApiKey` EF entity'sini `Create()`/`Revoke()` davranışlarıyla oluştur — scope taşımaz (src/others/Identity.Server/Data/ApiKey.cs)
- [x] T004 [P] `UserScope` EF entity'sini oluştur `(UserId, Scope)` (src/others/Identity.Server/Data/UserScope.cs)
- [x] T005 ApplicationDbContext'e `DbSet<ApiKey>` + `DbSet<UserScope>` + unique index'ler (KeyHash; UserId+Scope) ekle (src/others/Identity.Server/Data/ApplicationDbContext.cs)
- [x] T006 ApiKeys + UserScopes için EF migration üret (src/others/Identity.Server/Data/Migrations/ApplicationDb/)
- [x] T007 `ApiKeyService`: anahtar üretimi (`umk_`+32 rastgele bayt), SHA-256 hash, hash ile çözümleme + UserScopes okuma (src/others/Identity.Server/ApiKeys/ApiKeyService.cs)
- [x] T008 [P] `ApiKeyAuthenticationOptions` (header adı, resolve adresi, iç-secret) oluştur (src/others/Common/Auths/ApiKeyAuthenticationOptions.cs)
- [x] T009 `ApiKeyAuthenticationHandler`: X-User-Key → resolve → ClaimsPrincipal(sub/email/ad/scope); yok→NoResult, geçersiz→Fail (src/others/Common/Auths/ApiKeyAuthenticationHandler.cs)
- [x] T010 Present-but-invalid-key middleware: header var + authenticated değil → 401 (src/others/Common/Middleware/InvalidApiKeyRejectionMiddleware.cs)
- [x] T011 ApiKey şeması + resolve HttpClient'ı ekle (as-built: forward policy scheme yerine EAGER middleware — bkz. research D4/D5 as-built notu) (src/others/Common/Extensions/ApiKeyAuthenticationExtension.cs)
- [x] T012 [P] Test projesi kur (gerekli xUnit/Shouldly sürümlerini Directory.Packages.props'a ekle — CPM) + birim testleri: hash determinizmi, üretim benzersizliği, `ApiKey.Revoke` idempotency (tests/Identity.Server.Tests/)

**Checkpoint**: Entity'ler, auth handler ve forward şeması hazır — story'ler başlayabilir

---

## Phase 3: User Story 1 - Tek kalıcı anahtarla yazma (Priority: P1) 🎯 MVP

**Goal**: Dış tüketici tek `UserKey` ile bir gerçek kullanıcının adına yazma yapar.

**Independent Test**: Anahtarla yazma başarılı; anahtarsız/geçersiz anahtarla reddedilir.

- [x] T013 [US1] Resolve endpoint `POST /api/keys/resolve` (iç-secret guard) → userId/email/ad/scopes döndür (src/others/Identity.Server/ApiKeys/ApiKeyEndpoints.cs)
- [x] T014 [US1] Identity.Server Program.cs'te ApiKeyService kaydı + endpoint map + servisleri DI'a ekle (src/others/Identity.Server/Program.cs)
- [x] T015 [US1] ApiKey şemasını devreye al (AddApiKeyAuthentication + UseApiKeyAuthentication) — **8 servisin hepsi** yapıldı + her birine iç-secret config (her servis Program.cs + appsettings.Development.json)
- [x] T016 [P] [US1] Handler birim testi (stub HttpMessageHandler): 200→Success+scope claim, 401→Fail, header yok→NoResult (tests/Common.Tests/ApiKeyAuthenticationHandlerTests.cs, 3/3)
- [x] T017 [US1] quickstart Senaryo 1–3 CANLI doğrulandı: valid key→add_to_cart isSuccess (doğru user'a); bad key→401; anahtarsız write→scope reddi

**Checkpoint**: US1 tek başına çalışır ve test edilebilir — MVP

---

## Phase 4: User Story 2 - Anahtar yaşam döngüsü (Priority: P2)

**Goal**: Operatör anahtar üretir ve anında iptal edebilir.

**Independent Test**: Üretilen anahtar çalışır; iptal sonrası aynı anahtar reddedilir.

- [x] T018 [US2] Issue endpoint `POST /api/keys` (`apikeys.manage`) → ham anahtarı **bir kez** döndür (src/others/Identity.Server/ApiKeys/ApiKeyEndpoints.cs)
- [x] T019 [US2] Revoke endpoint `POST /api/keys/{id}/revoke` (`apikeys.manage`), idempotent (src/others/Identity.Server/ApiKeys/ApiKeyEndpoints.cs)
- [ ] T020 [P] [US2] Birim testi: iptalli anahtar çözümleme reddi; aynı kullanıcının iki anahtarının bağımsızlığı (tests/Identity.Server.Tests/)
- [x] T021 [US2] Senaryo 6 CANLI doğrulandı: revoke→204, resolve sonrası→401, MCP write sonrası→401 (anında). Çoklu-anahtar bağımsızlığı (S8) ayrı koşulmadı.

**Checkpoint**: US1 + US2 birlikte bağımsız çalışır

---

## Phase 5: User Story 4 - Kayıtta scope seçimi (Priority: P2)

**Goal**: Kullanıcı kayıtta operatör-listesinden yetki seçer; anahtar bunları miras alır.

**Independent Test**: Salt-okuma seçenin anahtarı yazamaz; yazma seçenin anahtarı yazar.

- [x] T022 [US4] Account/Create ekranına operatör-sunulan scope checkbox listesi + InputModel ekle (src/others/Identity.Server/Pages/Account/Create/Index.cshtml + .cshtml.cs)
- [x] T023 [US4] Kayıt anında seçili scope'ları UserScopes'a yaz; yalnızca operatör-sunulan kümeyi kabul et (src/others/Identity.Server/Pages/Account/Create/Index.cshtml.cs)
- [ ] T024 [P] [US4] Birim testi: liste-dışı scope reddedilir; seçim kalıcılaşır (tests/Identity.Server.Tests/)
- [x] T025 [US4] CANLI doğrulandı: kayıtta basket.write seçimi→UserScopes'a yazıldı; resolve o scope'u döndü; scope'suz (anahtarsız) write reddedildi

**Checkpoint**: Yetki kaynağı kullanıcı onayıyla kurulur

---

## Phase 6: User Story 3 - Anonim okuma (Priority: P3)

**Goal**: Anahtarsız okuma çalışır.

**Independent Test**: Hiç anahtar göndermeden okuma 200 döner.

- [x] T026 [US3] **DÜZELTİLDİ**: 6 MCP read Agent slice'ı `[RequiredScope(…Read)]` taşıyordu (get_basket/get_orders/get_product/search_products/stock/payments) → hepsinden KALDIRILDI. (İlk grep eksikti, hatalı "gerek yok" denmişti.) Böylece read'ler gerçekten anonim + write-only key sahibi kendi verisini okuyabilir.
- [x] T027 [US3] CANLI doğrulandı: anonim catalog search_products → düzgün Result (auth reddi değil); write-only key sahibi get_basket ile kendi sepetini okudu

**Checkpoint**: Tüm story'ler bağımsız çalışır

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T028 [P] Drift varsa spec-kit dokümanlarını as-built ile hizala (specs/004-external-mcp-userkey/)
- [x] T029 Admin uçları **apikeys.manage** scope'a taşındı (JWT bearer + policy; apikeys.admin client_credentials). Resolve iç-secret'ta (karar). X-Internal-Secret karşılaştırması **sabit-zamanlı** (SHA-256 + FixedTimeEquals). Key hash lookup SQL-indexli (kod-içi karşılaştırma yok).
- [ ] T030 quickstart Senaryo 7 (süresizlik) + tüm senaryoları Aspire üzerinde uçtan uca çalıştır
- [x] T031 [P] Anayasa V'e PATCH amendment (v1.1.1): JWT-olmayan custom şema meşru, yetki scope-tabanlı kalır (.specify/memory/constitution.md) — remediation'da yapıldı

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: bağımsız başlar
- **Foundational (Phase 2)**: Setup sonrası; TÜM story'leri bloklar
- **User Stories (Phase 3–6)**: Foundational sonrası; öncelik sırası P1 → P2 → P2 → P3
- **Polish (Phase 7)**: istenen story'ler bitince

### Key task dependencies

- T005 → T003, T004 · T006 → T005 · T007 → T003, T004 · T009 → T008 · T010/T011 → T009
- T013 → T007 · T015 → T011, T010 · T018/T019 → T007 · T023 → T004

### User Story Dependencies

- **US1 (P1)**: Foundational sonrası; diğerlerine bağlı değil. UserScopes satırı test için seed edilir.
- **US2 (P2)**: Foundational sonrası; US1'den bağımsız (anahtar üretim/iptal).
- **US4 (P2)**: Foundational sonrası; UserScopes'u self-service doldurur (US1 seed yerine gerçek akış).
- **US3 (P3)**: Foundational sonrası; büyük ölçüde doğrulama (read yüzeyi zaten anonim-uyumlu).

### Within a story

- Endpoint'ten önce service; principal kurmadan önce resolve; test davranıştan önce/yanında yazılır.

---

## Parallel Opportunities

- Setup: T002 [P]
- Foundational: T003, T004, T008, T012 [P] (farklı dosyalar)
- US1: T016 [P] · US2: T020 [P] · US4: T024 [P] · Polish: T028, T031 [P]
- Foundational bitince US1/US2/US4/US3 farklı geliştiricilerce paralel yürütülebilir

## Parallel Example: Foundational

```bash
Task: "ApiKey entity + Create/Revoke — src/others/Identity.Server/Data/ApiKey.cs"
Task: "UserScope entity — src/others/Identity.Server/Data/UserScope.cs"
Task: "ApiKeyAuthenticationOptions — src/others/Common/Auths/ApiKeyAuthenticationOptions.cs"
Task: "Test projesi + hash/üretim/Revoke testleri — tests/Identity.Server.Tests/"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational (KRİTİK) → 3. Phase 3 US1
4. **DUR ve DOĞRULA**: US1'i bağımsız test et (quickstart 1–3) → demo

### Incremental Delivery

1. Setup + Foundational → temel hazır
2. US1 (MVP: anahtarla yazma) → doğrula → demo
3. US2 (üret/iptal) → doğrula → demo
4. US4 (kayıtta scope seçimi) → doğrula → demo
5. US3 (anonim okuma doğrulama) → doğrula → demo

---

## Notes

- Anahtar ham değeri asla saklanmaz/yeniden gösterilmez (yalnızca SHA-256 hash).
- Servis kodları değişmez; yalnızca ortak infra üzerinden ApiKey şeması kazanılır.
- Gateway `/mcp` sabit pass-through — dokunma.
- Her task veya mantıklı grup sonrası commit; checkpoint'lerde story'yi bağımsız doğrula.