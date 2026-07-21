---
description: "Task list for External MCP UserKey"
---

# Tasks: External MCP UserKey

**Input**: Design documents from `/specs/004-external-mcp-userkey/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Anayasa kalite-gate'i gereği yeni davranış (hash/üretim/iptal/scope) için hedefli
**saf birim testleri** dahil edildi (xUnit + Shouldly; host harness yok).

**Organization**: Görevler user story'lere göre gruplu; her story bağımsız test edilebilir artımdır.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1..US4 (spec.md); Setup/Foundational/Polish etiketsiz

## Path notu

Değişiklikler iki yerde: `src/others/Identity.Server/` (kalıcılık + uçlar + kayıt ekranı) ve
`src/others/Common/` (paylaşılan custom auth). Servis Program.cs'leri yalnızca şemayı devreye alır.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Konfigürasyon ve scope tanımları

- [ ] T001 Config.cs'e `apikeys.manage` ApiScope + operatör-sunulan scope listesi sabiti ekle; `apikeys.manage`'i bir admin client'a (veya bff'e) grant et — issue/revoke çağrılabilsin (src/others/Identity.Server/Config.cs)
- [ ] T002 [P] Resolve iç-secret + header adı ("X-User-Key"/"X-Internal-Secret") config anahtarlarını ekle (Identity.Server appsettings.*.json + Common option)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm story'lerin dayandığı entity + auth altyapısı

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz

- [ ] T003 [P] `ApiKey` EF entity'sini `Create()`/`Revoke()` davranışlarıyla oluştur — scope taşımaz (src/others/Identity.Server/Data/ApiKey.cs)
- [ ] T004 [P] `UserScope` EF entity'sini oluştur `(UserId, Scope)` (src/others/Identity.Server/Data/UserScope.cs)
- [ ] T005 ApplicationDbContext'e `DbSet<ApiKey>` + `DbSet<UserScope>` + unique index'ler (KeyHash; UserId+Scope) ekle (src/others/Identity.Server/Data/ApplicationDbContext.cs)
- [ ] T006 ApiKeys + UserScopes için EF migration üret (src/others/Identity.Server/Data/Migrations/ApplicationDb/)
- [ ] T007 `ApiKeyService`: anahtar üretimi (`umk_`+32 rastgele bayt), SHA-256 hash, hash ile çözümleme + UserScopes okuma (src/others/Identity.Server/ApiKeys/ApiKeyService.cs)
- [ ] T008 [P] `ApiKeyAuthenticationOptions` (header adı, resolve adresi, iç-secret) oluştur (src/others/Common/Auths/ApiKeyAuthenticationOptions.cs)
- [ ] T009 `ApiKeyAuthenticationHandler`: X-User-Key → resolve → ClaimsPrincipal(sub/email/ad/scope); yok→NoResult, geçersiz→Fail (src/others/Common/Auths/ApiKeyAuthenticationHandler.cs)
- [ ] T010 Present-but-invalid-key middleware: header var + authenticated değil → 401 (src/others/Common/Middleware/InvalidApiKeyRejectionMiddleware.cs)
- [ ] T011 `AuthenticationExtension`'a ApiKey şeması + forward "smart" policy scheme'i default olarak ekle (header'a göre ApiKey/Bearer) (src/others/Common/Extensions/AuthenticationExtension.cs)
- [ ] T012 [P] Test projesi kur (gerekli xUnit/Shouldly sürümlerini Directory.Packages.props'a ekle — CPM) + birim testleri: hash determinizmi, üretim benzersizliği, `ApiKey.Revoke` idempotency (tests/Identity.Server.Tests/)

**Checkpoint**: Entity'ler, auth handler ve forward şeması hazır — story'ler başlayabilir

---

## Phase 3: User Story 1 - Tek kalıcı anahtarla yazma (Priority: P1) 🎯 MVP

**Goal**: Dış tüketici tek `UserKey` ile bir gerçek kullanıcının adına yazma yapar.

**Independent Test**: Anahtarla yazma başarılı; anahtarsız/geçersiz anahtarla reddedilir.

- [ ] T013 [US1] Resolve endpoint `POST /api/keys/resolve` (iç-secret guard) → userId/email/ad/scopes döndür (src/others/Identity.Server/ApiKeys/ApiKeyEndpoints.cs)
- [ ] T014 [US1] Identity.Server Program.cs'te ApiKeyService kaydı + endpoint map + servisleri DI'a ekle (src/others/Identity.Server/Program.cs)
- [ ] T015 [US1] Servislerde ApiKey şemasını devreye al: resolve'a typed HttpClient + güncel AuthenticationExtension çağrısı + invalid-key middleware pipeline'a (her servis Program.cs)
- [ ] T016 [P] [US1] Handler birim testi: 200→Success(scope claim'leri), 401→Fail, header yok→NoResult (tests/Identity.Server.Tests/)
- [ ] T017 [US1] quickstart Senaryo 1–3 doğrula (anahtarla yazma ok; anahtarsız 401; kurcalanmış 401)

**Checkpoint**: US1 tek başına çalışır ve test edilebilir — MVP

---

## Phase 4: User Story 2 - Anahtar yaşam döngüsü (Priority: P2)

**Goal**: Operatör anahtar üretir ve anında iptal edebilir.

**Independent Test**: Üretilen anahtar çalışır; iptal sonrası aynı anahtar reddedilir.

- [ ] T018 [US2] Issue endpoint `POST /api/keys` (`apikeys.manage`) → ham anahtarı **bir kez** döndür (src/others/Identity.Server/ApiKeys/ApiKeyEndpoints.cs)
- [ ] T019 [US2] Revoke endpoint `POST /api/keys/{id}/revoke` (`apikeys.manage`), idempotent (src/others/Identity.Server/ApiKeys/ApiKeyEndpoints.cs)
- [ ] T020 [P] [US2] Birim testi: iptalli anahtar çözümleme reddi; aynı kullanıcının iki anahtarının bağımsızlığı (tests/Identity.Server.Tests/)
- [ ] T021 [US2] quickstart Senaryo 6 & 8 doğrula (iptal ≤5sn etkili; çoklu anahtar bağımsız)

**Checkpoint**: US1 + US2 birlikte bağımsız çalışır

---

## Phase 5: User Story 4 - Kayıtta scope seçimi (Priority: P2)

**Goal**: Kullanıcı kayıtta operatör-listesinden yetki seçer; anahtar bunları miras alır.

**Independent Test**: Salt-okuma seçenin anahtarı yazamaz; yazma seçenin anahtarı yazar.

- [ ] T022 [US4] Account/Create ekranına operatör-sunulan scope checkbox listesi + InputModel ekle (src/others/Identity.Server/Pages/Account/Create/Index.cshtml + .cshtml.cs)
- [ ] T023 [US4] Kayıt anında seçili scope'ları UserScopes'a yaz; yalnızca operatör-sunulan kümeyi kabul et (src/others/Identity.Server/Pages/Account/Create/Index.cshtml.cs)
- [ ] T024 [P] [US4] Birim testi: liste-dışı scope reddedilir; seçim kalıcılaşır (tests/Identity.Server.Tests/)
- [ ] T025 [US4] quickstart Senaryo 5 doğrula (salt-okuma kullanıcı yazınca 401)

**Checkpoint**: Yetki kaynağı kullanıcı onayıyla kurulur

---

## Phase 6: User Story 3 - Anonim okuma (Priority: P3)

**Goal**: Anahtarsız okuma çalışır.

**Independent Test**: Hiç anahtar göndermeden okuma 200 döner.

- [ ] T026 [US3] Read MCP tool'ları/`Features/Queries` handler'larının `[RequiredScope]` taşımadığını ve `/mcp` gateway route'unun policy'siz olduğunu doğrula; yanlış scope isteyen read'i düzelt (servisler)
- [ ] T027 [US3] quickstart Senaryo 4 doğrula (anonim okuma 200)

**Checkpoint**: Tüm story'ler bağımsız çalışır

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T028 [P] Drift varsa spec-kit dokümanlarını as-built ile hizala (specs/004-external-mcp-userkey/)
- [ ] T029 Güvenlik sertleştirme takip notu: resolve endpoint auth (mTLS/M2M), sabit-zamanlı hash karşılaştırma (research D8)
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