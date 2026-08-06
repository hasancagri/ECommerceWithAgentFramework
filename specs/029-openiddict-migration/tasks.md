# Tasks: OpenIddict Migrasyonu (Davranış Birebir)

**Input**: Design documents from `/specs/029-openiddict-migration/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/token-contract.md, quickstart.md

**Tests**: İlke VI kapsamı BOŞ — saf domain birimi yok (altyapı takası). Test task'ı üretilmedi; doğrulama canlı smoke (quickstart.md).

**Organization**: IdP çekirdeği tüm story'lerin ortak ön koşuludur; story fazları ağırlıkla sayfa portu + canlı doğrulamadır.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (paket ve temizlik)

- [X] T001 Branch aç: `029-openiddict-migration` (master'dan)
- [X] T002 Directory.Packages.props: Duende.IdentityServer/.AspNetIdentity/.EntityFramework çıkar; OpenIddict.AspNetCore + OpenIddict.EntityFrameworkCore 7.6.0 ekle (Duende.IdentityModel KALIR)
- [X] T003 src/others/Identity.Server/Identity.Server.csproj: Duende referanslarını OpenIddict referanslarıyla değiştir
- [X] T004 [P] Kalıntı sil: Data/Migrations/ (tamamı), keys/, Pages/{Consent,Grants,Diagnostics,Ciba,Device,ServerSideSessions,ExternalLogin,Redirect}, Pages/IdentityServerSuppressions.cs

## Phase 2: Foundational (IdP çekirdeği — TÜM story'leri bloklar)

- [X] T005 src/others/Identity.Server/Config.cs: Duende modellerini düz seed sabitlerine çevir — 3 client, 13 scope, scope→audience haritası (contracts/token-contract.md birebir)
- [X] T006 src/others/Identity.Server/Data/ApplicationDbContext.cs: UseOpenIddict() ekle; temiz tek Initial migration üret (Identity + ApiKeys + OpenIddict; DB Docker reset ile sıfırdan)
- [X] T007 src/others/Identity.Server/Program.cs: Duende kaydını sök; OpenIddict server+core+EF kur — SetIssuer(https://localhost:5001), DisableAccessTokenEncryption, dev signing/encryption sertifikaları, uçlar (authorize/token/userinfo/logout), grant'lar (code+PKCE, client_credentials, refresh_token), RegisterPromptValues("create"); açılış migration bloğu yalnız ApplicationDbContext
- [X] T008 src/others/Identity.Server/Connect/ScopeClaimArrayHandler.cs: R3 event handler — access token scope claim'ini tek string yerine değer dizisi yap (Duende paritesi; contracts sözleşmesi)
- [X] T009 src/others/Identity.Server/Connect/SeedHostedService.cs: açılışta idempotent client+scope seed (varsa güncelle; secret'lar bugünkü düz değerler)
- [X] T010 src/others/Identity.Server/Connect/AuthorizeEndpoint.cs: authorize akışı — login yönlendirme, prompt=create → /Account/Create (returnUrl korunur), implicit consent, claim destinasyonları (sub her yere; name/email/role id_token+access_token), SetResources (audience haritası)
- [X] T011 src/others/Identity.Server/Connect/TokenEndpoint.cs: token akışı — code+PKCE exchange, client_credentials (scope→audience), refresh_token grant
- [X] T012 [P] src/others/Identity.Server/Connect/UserinfoEndpoint.cs: userinfo — name/email/role döner (WebApp GetClaimsFromUserInfoEndpoint)
- [X] T013 [P] src/others/Identity.Server/Connect/LogoutEndpoint.cs: end-session — WebApp post-logout redirect çalışır
- [X] T014 Identity.Server derlenir hale getir: kalan dosyaların Duende using/type kalıntılarını temizle (Pages/Extensions.cs, Log.cs, Telemetry.cs, SecurityHeadersAttribute.cs gözden geçir; kullanılmayanı sil); GlobalUsings düzenini koru

**Checkpoint**: AppHost açılır (Docker volume sıfırlanmış), discovery döner (prompt_values_supported create içerir), order-saga token'ı decode'da sözleşmeye uyar (quickstart 1-2).

## Phase 3: User Story 1 — Kullanıcı girişi ve alışverişi (P1) 🎯 MVP

**Goal**: Kayıtlı kullanıcı e-posta/şifreyle girer; sepet/sipariş/oturum yenileme birebir.

**Independent Test**: quickstart 4, 5, 8 (kullanıcı quickstart 7 register'ıyla oluşur).

- [X] T015 [US1] Pages/Account/Login port: Duende interaction service çıkar; SignInManager + returnUrl doğrulaması; RememberMe süresi korunur (Program.cs cookie config)
- [X] T016 [P] [US1] Pages/Account/Logout port: SignOutAsync + OpenIddict end-session'a devir
- [X] T017 [P] [US1] Pages/Account/AccessDenied gözden geçir: Duende bağımlılığı varsa temizle
- [X] T018 [US1] Canlı doğrulama: login → sepete ekle (gRPC stok rezervi) → sipariş PASS (2026-08-06); scope-array bug bulundu+düzeltildi; token-refresh edge ayrıca doğrulanmadı

**Checkpoint**: US1 uçtan uca PASS — MVP hazır.

## Phase 4: User Story 2 — Yeni kullanıcı kaydı (P2)

**Goal**: prompt=create ile kayıt sayfası açılır; hesap açılır; rol atanmaz.

**Independent Test**: quickstart 7.

- [X] T019 [US2] Pages/Account/Create port: UserManager.CreateAsync + otomatik login + returnUrl'e dönüş; Duende tipleri çıkar
- [X] T020 [US2] Canlı doğrulama: temiz tarayıcı → "Kayıt ol" → hesap + alışveriş; token'da rol claim'i YOK (quickstart 7, FR-011)

## Phase 5: User Story 3 — Anonim gezinme (P2)

**Goal**: Login'siz vitrin/ürün gezinme sürer (kod değişikliği beklenmez; M2M anonim okuma kanıtı).

**Independent Test**: quickstart 3.

- [X] T021 [US3] Canlı doğrulama: oturumsuz tarayıcıda ana sayfa + liste + detay; hiçbir sayfa login istemez (quickstart 3)

## Phase 6: User Story 4 — Makine akışları (P1)

**Goal**: Saga, gRPC forwarding, ChatAgent, ApiKeys admin — sıfır dokunuşla çalışır (kanıt fazı).

**Independent Test**: quickstart 6, 9, 10, 11.

- [X] T022 [US4] Canlı doğrulama: checkout saga uçtan uca — sipariş başarılı (2026-08-06, T018 sipariş akışıyla); stok düşüm + sepet temizliği saga tamamlanmasıyla dolaylı doğrulandı
- [X] T023 [P] [US4] Canlı doğrulama: ChatAgent per-user MCP tool çağrısı (quickstart 9)
- [X] T024 [P] [US4] Canlı doğrulama: yetkisiz istek 401/403 (quickstart 10) + ApiKeys issue/revoke (quickstart 11)

## Phase 7: Polish & Cross-Cutting

- [X] T025 dotnet build + dotnet test temiz; git diff --stat yalnız Identity.Server + Directory.Packages.props + spec dosyaları (SC-003 kanıtı)
- [X] T026 quickstart.md 11 adımın tamamını tek oturumda koş (SC-002); sonuçları spec'e işle
- [X] T027 [P] CLAUDE.md hizala: Teknoloji Yığını Duende → OpenIddict + ASP.NET Identity; Yetkilendirme bölümü anayasa v1.6.0'a uydur

## Dependencies & Execution Order

- Phase 1 → Phase 2 → Phase 3 → Phase 4; Phase 5-6 Phase 2 sonrası koşulabilir ama login gerektirenler için Phase 3-4 önce.
- T005-T007 sıralı (aynı çekirdek); T008-T013 T007 sonrası; T012+T013 paralel; T014 hepsinden sonra.
- US2 (T019) Login port'undan bağımsız ama aynı klasöre dokunur — sırayla al.
- Quickstart 4 (login) kullanıcı ister → pratik sıra: T019 sonrası T018/T020 birlikte koşulabilir.
- Polish tüm story'ler PASS olduktan sonra.

## Parallel Opportunities

- T004 setup içinde diğerleriyle paralel; T012+T013; T016+T017; T023+T024; T027 T025/T026 ile paralel.

## Implementation Strategy

- MVP = Phase 1-3 + T019 (login testi için kullanıcı kaydı gerekir — temiz DB).
- Her checkpoint'te dur, canlı doğrula; smoke kırmızıysa ilerleme yok (davranış-birebir feature'ında yeşil smoke = tek ölçüt).