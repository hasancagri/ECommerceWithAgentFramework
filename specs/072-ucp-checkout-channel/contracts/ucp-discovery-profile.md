# Contract: UCP Keşif Profili + OAuth Metadata

Anonim (yetki gerektirmez) — platform kapıyı tanır. UCP `profile.json` (business variant) uyumlu.

## GET /.well-known/ucp

Mağaza business profili. Yanıt:

```json
{
  "ucp": {
    "capabilities": ["dev.ucp.shopping.checkout"],
    "extensions": ["dev.ucp.shopping.fulfillment", "dev.ucp.shopping.discount"],
    "payment_handlers": [
      { "spec": "<handler-id>", "available_instruments": [ /* mağaza/PG kabul ettiği araçlar */ ] }
    ]
  },
  "keys": [
    { "kid": "<sha256-thumbprint>", "kty": "EC", "crv": "P-256", "x": "...", "y": "..." }
    /* ve/veya kty=OKP crv=Ed25519 x=... */
  ]
}
```

- `capabilities` = checkout; `extensions` = fulfillment + discount (Q2 tam kapsam).
- `payment_handlers` = mağazanın kabul ettiği ödeme yöntemi ilanı (PG/iyzico sandbox arkada).
- `keys` = public JWKS (RFC 7517); yalnız public. Hem giden webhook imzasını hem gelen istek
  doğrulamasını besler. `kid` = JWK SHA-256 thumbprint (RFC 7638).

## GET /.well-known/oauth-authorization-server

OpenIddict'in zaten ürettiği OIDC/OAuth metadata (authorization/token endpoint, desteklenen scope'lar
— içinde `dev.ucp.shopping.checkout`, grant_types). Mümkünse mevcut Identity.Server metadata'sına
yönlendir/proxy'le; ayrı üretme.

## Notlar

- Scope `dev.ucp.shopping.checkout` → `KnownScopes` (kod-sahipli kapalı registry, İlke V).
- Platform kimliği = `client_credentials` makine istemcisi (`ucp-platform`), sentetik sipariş kullanıcısı.