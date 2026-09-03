# Kontrat: Protected Resource Metadata + 401 Challenge (RFC 9728 / RFC 6750)

Kapsam: korumalı MCP servisleri (basket, order, customer, payment). Storefront/catalog/stock MCP
anonim — bu kontratı SUNMAZ.

## 1. Metadata dokümanı

`GET /.well-known/oauth-protected-resource` (servis kökünde; gateway route'u dıştan erişilir kılar)

```json
{
  "resource": "http://localhost:<gw>/mcp/basket",
  "authorization_servers": ["https://localhost:<idp>"],
  "scopes_supported": ["basket.read", "basket.write"],
  "bearer_methods_supported": ["header"]
}
```

- `resource`: dış görünür MCP taban URI'si; gateway arkasında `X-Forwarded-Proto/Host` +
  yol öneki ile hesaplanır, trailing slash'sız.
- `authorization_servers`: Identity.Server issuer'ı (mevcut `IdentityOption.Address` ile AYNI
  string — issuer eşleşme kuralı).
- `scopes_supported`: yalnız o servisin dış-agent demetindeki scope'ları (data-model listesi).
- Uç anonimdir (keşif kimliksiz olmak zorunda), cache'lenebilir.

## 2. 401 challenge

Korumalı `/mcp` ucuna kimliksiz/geçersiz istek:

```
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Bearer resource_metadata="http://localhost:<gw>/.well-known/oauth-protected-resource/mcp/basket",
                         scope="basket.read basket.write"
```

- `resource_metadata` mutlak URI'dir ve 1. maddedeki dokümanı işaret eder (gateway yolu).
- Yetersiz scope'ta (401 değil) `403 + error="insufficient_scope"` + gerekli scope listesi.
- Mevcut UserKey (`X-User-Key`) isteği bu challenge'a düşmez (middleware önce çözer);
  header'sız + Bearer'sız istek düşer.

## 3. Doğrulama (resource server)

- JwtBearer: issuer = Identity.Server, audience = servisin mevcut audience değeri, scope policy
  mevcut `.RequireAuthorization(<ScopePolicy>)` zinciri.
- MCP spec gereği yalnız kendi audience'ını taşıyan token kabul edilir (mevcut Audience
  doğrulaması bunu sağlar; canlı testte teyit maddesi).