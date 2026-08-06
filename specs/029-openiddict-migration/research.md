# Research: OpenIddict Migrasyonu

**Date**: 2026-08-06 | **Plan**: [plan.md](plan.md)

## R1 — OpenIddict sürümü

- **Decision**: OpenIddict 7.6.0 (stable; `OpenIddict.AspNetCore` + `OpenIddict.EntityFrameworkCore`).
- **Rationale**: 8.0 henüz preview (8.0.0-preview.2, 2026-07); üretim davranışı için stable hat. 7.x .NET 10 üzerinde sorunsuz çalışır.
- **Alternatives**: 8.0-preview reddedildi (preview'a bağımlılık); 6.x reddedildi (7.x aktif hat, 6→7 migrasyon rehberi mevcut).

## R2 — Duende.IdentityModel kalır

- **Decision**: `Duende.IdentityModel` 8.1.0 aynen kalır (WebApp token handler'ları + 4 servis csproj'u).
- **Rationale**: Ticari lisans yalnız `Duende.IdentityServer` sunucusudur; IdentityModel Apache 2.0/ücretsizdir.
  Kaldırılması servis kodu değiştirir (SC-003 ihlali) ve hiçbir lisans kazancı yoktur.
- **Alternatives**: Elle HttpClient token istekleri — gereksiz yeniden yazım, reddedildi.

## R3 — Scope claim biçimi (EN KRİTİK RİSK)

- **Problem**: Duende access token'a scope'ları **çoklu claim** yazar; servisler `RequireClaim("scope", x)` ve
  `HasClaim("scope", x)` ile tek tek değer arar. OpenIddict varsayılanı RFC 9068: **tek boşluk-ayrık string** — bu biçimde
  tüm scope kontrolleri sessizce fail-closed olur (401/403).
- **Decision**: Sunucu tarafında Duende paritesi: OpenIddict token üretimine özel event handler eklenir
  (`GenerateTokenContext`, varsayılan claim ekleme handler'ından sonra sıralı) ve access token'daki `scope` claim'i
  tek string yerine **değer dizisine** çevrilir. Böylece JWT'de bugünkü gibi çoklu `scope` claim'i oluşur.
- **Rationale**: Tek dokunuş tüm tüketicileri kapsar (8 servis + ChatAgent MCP + Identity.Server'ın kendi
  `apikeys.manage` policy'si). SC-003 (sıfır servis değişikliği) korunur. Hata modu güvenlidir: handler bozulursa
  yetki fazla açılmaz, kapanır (smoke anında yakalar).
- **Alternatives**: (a) Common'daki iki kontrol noktasını boşluk-ayrık biçime dayanıklı yapmak — SC-003'ü ihlal eder,
  token sözleşmesini değiştirir (bilinmeyen tüketici riski); ertelenmiş iyileştirme olarak not edildi.
  (b) Servislerde `OnTokenValidated` claim bölme — 9 projeye dokunur, reddedildi.

## R4 — prompt=create (kayıt yolculuğu)

- **Decision**: `options.RegisterPromptValues("create")` ile prompt kabul edilir; authorize handler
  `request.HasPromptValue("create")` görünce `/Account/Create`'e yönlendirir (returnUrl korunarak).
- **Rationale**: OpenIddict 6.0+ prompt doğrulaması yapar ve `prompt_values_supported`'ı discovery'ye yazar;
  kayıtlı olmayan prompt değeri reddedilir — Duende'deki `CreateAccountUrl` davranışının birebir karşılığı elle kurulur.

## R5 — Access token biçimi ve doğrulama

- **Decision**: `DisableAccessTokenEncryption()` — düz imzalı JWT (OpenIddict varsayılanı şifreli token'dır; servislerin
  `JwtBearer`'ı çözemez). Dev imza/şifreleme sertifikaları `AddDevelopmentSigningCertificate` +
  `AddDevelopmentEncryptionCertificate` ile (id_token/authorization code için şifreleme anahtarı yine gerekir).
- **Header `typ: at+jwt`**: Servislerin `TokenValidationParameters.ValidTypes` set edilmediğinden kabul edilir; değişiklik gerekmez.
- **Issuer**: `options.SetIssuer("https://localhost:5001")` — bugünkü issuer birebir; HTTPS kuralı launchSettings ile sürer.

## R6 — Audience (aud) eşlemesi

- **Decision**: Duende `ApiResource` eşlemesi (scope → audience, 8 kayıt) Identity.Server'da sabit bir harita olarak tutulur;
  token üretiminde principal'a `SetResources(...)` ile verilen scope'ların audience'ları yazılır.
- **Rationale**: Servisler `ValidateAudience=true` ile kendi adını arar (`basket.api`...); çoklu-aud token bugünkü gibi çalışır.

## R7 — Store'lar ve veri

- **Decision**: `ApplicationDbContext.UseOpenIddict()`; TÜM eski migration'lar silinir, temiz tek Initial migration üretilir
  (Identity + ApiKeys + OpenIddict 4 tablo: Applications, Authorizations, Scopes, Tokens).
  `PersistedGrantDbContext` + Duende `keys/` klasörü silinir.
- **Veri**: DB Docker volume reset ile sıfırlanır (kullanıcı kararı 2026-08-06) — veri taşıma/drop migration'ı yok;
  kullanıcılar yeniden kayıt olur, ürünler feed'den dolar.
- **Seed**: 3 client + 13 scope açılışta idempotent seed edilir (`IHostedService`; varsa güncelle, yoksa yarat).
  Secret değerleri bugünkü düz değerlerle aynı (`webshop-secret`, `order-saga-secret`, `apikeys-admin-secret`) —
  WebApp/SagaTokenHandler config'i değişmez; OpenIddict secret'ları kendisi hash'ler.

## R8 — OIDC uçları ve sayfalar

- **Decision**: Uç yolları Duende konvansiyonuyla aynı kalır: `/connect/authorize`, `/connect/token`, `/connect/userinfo`,
  `/connect/logout`. WebApp path'leri discovery'den aldığı için yol adı zaten serbesttir; aynı tutmak teşhisi kolaylaştırır.
- **userinfo zorunlu**: WebApp `GetClaimsFromUserInfoEndpoint=true` — userinfo ucu name/email/role claim'lerini döndürür.
- **Claim destinasyonları**: `sub` her yere; `name`/`email`/`role` id_token + access_token'a (`SetDestinations`).
  Duende'deki `AlwaysIncludeUserClaimsInIdToken=true` + ApiResource `UserClaims` davranışının karşılığı.
- **Consent**: `ConsentTypes.Implicit` (bugünkü `RequireConsent=false`). Consent sayfası silinir.
- **Sayfa envanteri**: Login/Logout/Create/AccessDenied SignInManager'a port; Consent, Grants, Diagnostics, Ciba, Device,
  ServerSideSessions, ExternalLogin, Redirect silinir (kullanılmıyor; dış login sağlayıcı yok).
- **Refresh token**: `offline_access` izni + token ucunda refresh_token grant açık (WebApp `AuthenticatedHttpClientHandler`
  401'de refresh çağırıyor).

## Kaynaklar

- [NuGet: OpenIddict 7.6.0](https://www.nuget.org/packages/OpenIddict) · [OpenIddict profili](https://www.nuget.org/profiles/openiddict)
- [OpenIddict 6→7 migrasyon rehberi](https://documentation.openiddict.com/guides/migration/60-to-70)
- [RFC 9068 — JWT access token profili (scope biçimi)](https://datatracker.ietf.org/doc/html/rfc9068)
- [OpenIddict prompt doğrulaması PR #2197 (prompt_values_supported)](https://github.com/openiddict/openiddict-core/pull/2197)
- [OpenIddict 6.0 duyurusu (RegisterPromptValues, prompt=create)](https://kevinchalet.com/2024/12/17/openiddict-6-0-general-availability/)
- [OIDC prompt=create spec](https://openid.net/specs/openid-connect-prompt-create-1_0.html)