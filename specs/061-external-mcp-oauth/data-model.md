# Data Model: Dış Agent MCP Erişimi (OAuth) — 061

Yeni domain aggregate/tablosu YOK. Tüm kalıcı durum OpenIddict'in mevcut identityDb tablolarında
yaşar; bu doküman hangi varlığın nasıl KULLANILDIĞINI sabitler.

## OpenIddict Application (dış agent istemci kaydı)

DCR ucunun ürettiği kayıt. Alan kullanımı:

- **ClientId**: sunucu üretir (opak).
- **ClientType**: `public` (secret yok; PKCE zorunlu).
- **ConsentType**: `Explicit` — her yeni client için kullanıcı onayı şart (seed client'lar
  `Implicit` kalır, davranış değişmez).
- **RedirectUris**: yalnız izinli kalıplar — loopback (`http://localhost:*`, `http://127.0.0.1:*`)
  ve Claude callback'leri (`https://claude.ai/api/mcp/auth_callback`, claude.com eşleniği).
  Doğrulama saf `DcrRequestValidator` biriminde (test-first, İlke VI).
- **Permissions**: authorize + token endpoint, authorization_code + refresh_token grant,
  response_type=code, yalnız dış-agent scope demeti (aşağıda). `client_credentials` ASLA verilmez.
- **DisplayName**: DCR isteğindeki `client_name` (consent sayfasında gösterilir).

## Dış-agent scope demeti (kapalı liste — R4)

`storefront.read`, `basket.read`, `basket.write`, `order.read`, `order.write`, `customer.read`,
`payment.read` + `openid profile email offline_access`. Kaynak: `AuthorizationScopes` sabitleri
(Common/Utils/Constants). Yönetim scope'ları demete giremez. (`place_order` policy teyidi →
gerekirse `checkout.write` eklenir; tasks'ta doğrulama maddesi.)

## OpenIddict Authorization (consent kararı)

Kullanıcının hangi client'a hangi scope'ları onayladığının kaydı (OpenIddict yönetir). Consent
sayfası Onayla → kalıcı authorization; Reddet → hiçbir kayıt oluşmaz, hata dönüşü (SC-005).
İkinci bağlantıda aynı scope'lar onaylıysa consent ekranı atlanır (SC-003).

## OpenIddict Token (erişim + yenileme)

- Access token: JWT; `scope` claim (mevcut `ScopeClaimArrayHandler` yolu — TokenType URN tuzağı
  aynen geçerli), audience = scope→resource eşlemesinden servis audience'ları (R5).
- Refresh token: `offline_access` ile; sessiz yenileme (SC-003). Revocation ucu açılır (FR-009).
- Token'lar agent tarafında saklanır; sunucu tarafında oturum/transkript durumu tutulMAZ.

## Keşif dokümanları (kalıcı olmayan, hesaplanan)

- **Protected Resource Metadata** (servis başına): `resource` (dış görünür MCP URI'si —
  forwarded header'lardan), `authorization_servers: [Identity.Server issuer]`,
  `scopes_supported: [servisin demet-içi scope'ları]`. Kontrat: `contracts/protected-resource-metadata.md`.
- **AS discovery**: OpenIddict `/.well-known/openid-configuration` + eklenen
  `registration_endpoint` alanı. DCR kontratı: `contracts/dcr-register.md`.

## Durum geçişleri (bağlantı yaşam döngüsü)

```
[bağlantı yok] --connector ekle + 401 keşif--> [DCR: client kaydı]
  --tarayıcı: login (+register) + consent Onayla--> [bağlı: access+refresh]
  --access süresi doldu--> [sessiz refresh] --> [bağlı]
  --consent Reddet--> [bağlantı yok] (yan etki yok)
  --disconnect/revoke--> [bağlantı yok] (sonraki çağrılar 401)
```