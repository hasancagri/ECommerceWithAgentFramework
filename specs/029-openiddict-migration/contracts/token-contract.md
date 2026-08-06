# Token / İstemci / Uç Sözleşmesi

**Date**: 2026-08-06 | **Plan**: [../plan.md](../plan.md)

Bu sözleşme "davranış birebir" hedefinin ölçütüdür: geçiş sonrası üretilen token'lar bu şekli korumak ZORUNDA.

## Access token claim sözleşmesi (servislerin doğruladığı)

| Claim | Biçim | Tüketici |
|-------|-------|----------|
| `iss` | `https://localhost:5001` (birebir) | Tüm servisler (`ValidateIssuer=true`) |
| `aud` | Scope'ların audience'ları; çoklu değer | Tüm servisler (`ValidateAudience=true`, kendi adını arar) |
| `scope` | **Çoklu claim / JSON dizi** — boşluk-ayrık TEK string DEĞİL | `RequireClaim("scope", x)` + `ScopeAuthorizationMiddleware` |
| `sub` | Kullanıcı Id (client credentials'ta client kimliği) | `CurrentUser.Load` |
| `name`, `email` | Kullanıcı token'larında mevcut | `CurrentUser.Load`, WebApp |
| `role` | Bu feature'da BOŞ (rol atanmıyor); alan Feature 2'de dolar | WebApp `RoleClaimType` |
| header `typ` | `at+jwt` kabul (servisler ValidTypes kısıtlamaz) | JwtBearer |
| Şifreleme | YOK — düz imzalı JWT (`DisableAccessTokenEncryption`) | JwtBearer |

## id_token sözleşmesi (WebApp OIDC)

- `sub`, `name`, `email`, `role` id_token'da taşınır (bugünkü `AlwaysIncludeUserClaimsInIdToken=true` davranışı).
- userinfo ucu aynı kullanıcı claim'lerini döndürür (`GetClaimsFromUserInfoEndpoint=true`).

## İstemci kayıtları (seed; secret ve URI'lar birebir)

| ClientId | Grant'lar | Scope'lar | Not |
|----------|-----------|-----------|-----|
| `ecommerce.bff` | authorization code + PKCE, client credentials, refresh_token | openid, profile, email, roles, offline_access + 12 servis scope'u | Redirect: `https://localhost:7042/signin-oidc`; logout: `/signout-callback-oidc`; consent yok |
| `order-saga` | client credentials | stock.reserve, basket.write | SagaTokenHandler; secret `order-saga-secret` |
| `apikeys.admin` | client credentials | apikeys.manage | Identity.Server kendi Bearer policy'si doğrular |

## Uç sözleşmesi

| Uç | Yol | Not |
|----|-----|-----|
| Discovery | `/.well-known/openid-configuration` | WebApp/servisler Authority'den çeker; `prompt_values_supported` "create" içerir |
| Authorize | `/connect/authorize` | `prompt=create` → `/Account/Create` yönlendirmesi (returnUrl korunur) |
| Token | `/connect/token` | code+PKCE, client_credentials, refresh_token grant'ları |
| UserInfo | `/connect/userinfo` | name/email/role döner |
| Logout | `/connect/logout` | WebApp post-logout redirect'i çalışır |

## Değişmeyen dış sözleşmeler

- `IdentityOption` (Address/Issuer/Audience) config şeması ve değerleri — hiçbir serviste değişmez.
- ApiKeys admin uçları (`X-Internal-Secret` + `apikeys.manage` Bearer) — davranış aynen.
- gRPC `BearerForwardingHandler` / ChatAgent `TokenInjectingHandler` — token'ı opak taşır, biçim korunduğundan etkilenmez.