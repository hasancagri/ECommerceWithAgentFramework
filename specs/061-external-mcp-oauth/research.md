# Research: Dış Agent MCP Erişimi (OAuth) — 061

**Date**: 2026-09-03 · Kaynaklar: MCP authorization spec (modelcontextprotocol.io), Claude Code
GitHub issue'ları (#3273, #52638, #38102), repo kod taraması (Common/Auths, Identity.Server, gateway).

## R1 — Keşif mekanizması: RFC 9728 protected-resource metadata

- **Decision**: MCP ucu açan her servis `/.well-known/oauth-protected-resource` dokümanı sunar
  (`resource`, `authorization_servers: [Identity.Server]`, `scopes_supported`); korumalı MCP uçları
  kimliksiz istekte `401 + WWW-Authenticate: Bearer resource_metadata="...", scope="..."` döner.
  İkisi de `Common`'da tek extension; servis Program.cs'te tek satırla açar.
- **Rationale**: MCP spec'i RFC 9728'i sunucu için ZORUNLU kılar; istemci keşfi 401 header'ından
  başlar. Bugün `MapMcp("/mcp")` hiç auth taşımıyor (Basket Program.cs:93) → challenge da yok.
- **Alternatives**: Gateway'de merkezi metadata → reddedildi: servis kendi resource kimliğini
  bilmeli (FR-002), gateway ince kalmalı; `resource` değeri forwarded header'lardan türetilir.

## R2 — Claude Code istemci kaydı: DCR ZORUNLU

- **Decision**: Identity.Server'a küçük bir RFC 7591 Dynamic Client Registration ucu yazılır
  (`POST /connect/register`, anonim): OpenIddict application üretir (public client, PKCE zorunlu,
  auth code + refresh grant). AS discovery dokümanına `registration_endpoint` alanı eklenir
  (OpenIddict event handler ile).
- **Rationale**: Claude Code DCR'siz auth server'ı REDDEDER ("does not support dynamic client
  registration" — issue #3273/#52638/#38102; elle clientId verme yolu yok). OpenIddict 7.6'da DCR
  built-in değil; `IOpenIddictApplicationManager` ile ince uç yeterli.
- **Alternatives**: Elle client seed → Claude Code bunu kullanamıyor, düştü. Client ID Metadata
  Documents (yeni draft) → Claude Code bugün DCR kullanıyor, draft'a yatırım erken.
- **Güvenlik sınırları**: redirect URI yalnız loopback (`http://localhost:*`, `http://127.0.0.1:*`)
  + Claude callback'leri (`https://claude.ai/api/mcp/auth_callback`, `https://claude.com/...`);
  izinli scope'lar yalnız dış-agent demeti (R4); client_credentials verilmez.

## R3 — Kimlik doğrulama: mevcut JwtBearer + seçici zorlama

- **Decision**: Kullanıcıya bağlı MCP servisleri (basket, order, customer, payment) `MapMcp`'ye
  `.RequireAuthorization()` alır + Bearer challenge'ı resource_metadata parametresiyle zenginleşir.
  Anonim gezinme yüzeyleri (storefront, catalog, stock) MCP'de anonim KALIR (İlke V: anonim
  gezinme meşru) — bu servislerde connector auth'suz çalışır, OAuth hiç tetiklenmez.
- **Rationale**: Servisler JwtBearer'ı zaten doğruluyor (Authority + Audience + scope policy;
  AuthenticationExtension.cs:16). ChatAgent PUBLIC personası storefront MCP'yi token'sız
  kullanıyor — storefront'a auth koymak regresyon yaratırdı (SC-004).
- **UserKey etkileşimi**: ApiKeyAuthenticationMiddleware header yokken pass-through
  (Middleware.cs:11-24, Handler NoResult) → Bearer yolu ile çakışmaz; UserKey'li istek
  `RequireAuthorization`'ı ApiKey şemasıyla geçer. İki yol yan yana (FR-007).

## R4 — Dış agent scope demeti (FR-006)

- **Decision**: `AuthorizationScopes`'tan (kapalı registry): `storefront.read`, `basket.read`,
  `basket.write`, `order.read`, `order.write`, `customer.read`, `payment.read` + kimlik scope'ları
  `openid profile email offline_access`. Yönetim scope'ları (IdentityRolesManage, *Write admin
  yüzeyleri) DCR client'larına KAPALI.
- **Rationale**: Alışveriş yaşam döngüsünün tamamı (arama→sepet→sipariş→takip) + sessiz yenileme
  (offline_access, SC-003). ChatAgent ASSISTANT personasının tool yüzeyiyle örtüşür.
- **Doğrulanacak**: `place_order` handler'ının istediği scope policy (OrderWrite mi CheckoutWrite
  mi) — implement sırasında policy'den teyit; gerekirse demete CheckoutWrite eklenir.

## R5 — Audience + resource parametresi (RFC 8707)

- **Decision**: Audience, mevcut OpenIddict scope→resource eşlemesiyle sağlanır (scope'lu token
  ilgili servisin audience'ını taşır; JwtBearer `Audience` doğrulaması zaten açık). Claude'un
  gönderdiği `resource` parametresinin authorize/token uçlarında hata üretmediği canlı doğrulanır.
- **Rationale**: MCP spec'i audience doğrulamasını ZORUNLU kılar; mevcut zincir bunu karşılıyor
  (ChatAgent Bearer akışı bugün audience'la çalışıyor). OpenIddict 7.6'nın `resource` param
  davranışı sürüme bağlı → canlı test maddesi, tasarım riski değil.

## R6 — Consent: tek sayfa, Explicit

- **Decision**: DCR ile kayıt olan client'lar `ConsentTypes.Explicit` alır; Identity.Server'a tek
  bir consent sayfası eklenir (istenen scope listesi + Onayla/Reddet). Seed'li mevcut client'lar
  (ecommerce.bff vb.) Implicit kalır — davranışları değişmez.
- **Rationale**: FR-005 kullanıcı onayını şart koşar; bugün consent sayfası YOK (Config
  ConsentType=Implicit, SeedHostedService.cs:101). Dış agent üçüncü-taraf istemcidir; sessiz izin
  kabul edilemez. Spec varsayımı bu istisnayı önceden tanımladı ("tek sayfa").
- **Alternatives**: Implicit bırak → FR-005/SC-005 düşer, reddedildi.

## R7 — Token yaşam döngüsü: refresh var, revocation ucu eklenir

- **Decision**: DCR client'larına refresh token grant verilir (`offline_access`); OpenIddict
  revocation endpoint'i açılır (FR-009). Access token ömrü mevcut varsayılanda kalır.
- **Rationale**: SC-003 (ikinci oturumda ekran yok) refresh'e dayanır; flow zaten seed'li
  (Program.cs:67-69). Revocation bugün kapalı — tek satırlık endpoint açımı + doküman.

## R8 — Topoloji: gateway üzerinden, localhost + Claude Code önce

- **Decision**: Claude Code connector'ları gateway yollarına bağlanır
  (`http://localhost:<gw>/mcp/<servis>`); gateway'e `/.well-known/oauth-protected-resource`
  yönlendirme route'ları eklenir. HTTPS/tünel bu dilim dışı (localhost'a Claude Code http kabul eder).
- **Rationale**: Gateway "tek giriş" mimari duruşu + tünel aşamasında tek public host ihtiyacı.
  Identity.Server zaten HTTPS (issuer eşleşmesi mevcut kural).

## Çözülen belirsizlikler özeti

| Soru | Karar |
|---|---|
| Claude client kaydı nasıl? | DCR ucu yazılacak (zorunlu; elle seed yetmiyor) |
| Hangi MCP'ler korunacak? | basket/order/customer/payment; storefront/catalog/stock anonim |
| Scope demeti? | storefront.read, basket.r/w, order.r/w, customer.read, payment.read + OIDC + offline_access |
| Consent? | DCR client'ları Explicit + tek consent sayfası; seed client'lar Implicit kalır |
| Audience? | Mevcut scope→resource eşlemesi; resource param canlı doğrulanır |
| UserKey çakışması? | Yok — middleware header yokken pass-through (kod teyitli) |