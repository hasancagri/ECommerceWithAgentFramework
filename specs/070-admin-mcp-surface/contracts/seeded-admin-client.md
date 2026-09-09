# Contract: Seed'li Admin OAuth İstemcisi (070 / FR-011)

Ev: Identity.Server `Config.Clients` + `SeedHostedService` (idempotent boot seed). DCR yüzeyinin
TAMAMEN dışında; `ExternalAgentDefaults`/`DcrRequestValidator` DEĞİŞMEZ.

| Alan | Değer |
|---|---|
| ClientId | `external-admin-agent` |
| İstemci tipi | public (secret yok) + PKCE zorunlu |
| Grant'ler | authorization_code, refresh_token (client_credentials YOK) |
| Consent | Implicit (seed istemci; mağaza sahibinin kendi aracı) |
| Redirect URI'ler | `https://claude.ai/api/mcp/auth_callback`, `https://claude.com/api/mcp/auth_callback`, loopback (`http://localhost:*`, `http://127.0.0.1:*`) |
| Identity scope | openid, profile |
| API scope tavanı | storefront.read, catalog.write, stock.write, merchant.credentials.write |
| Gerçek yetki | tavan ∩ kullanıcının ROL demeti (030 refresh'te yeniden süzülür) — admin-olmayan kullanıcı yönetim scope'u ALAMAZ |

## Doğrulama senaryoları

- Admin, Claude Desktop'a `/mcp-admin/catalog` ekler → 401 + resource metadata → bu istemciyle PKCE
  login → token `catalog.write` taşır → tool çalışır.
- DCR `/connect/register` üzerinden `catalog.write` istenirse: scope sessizce düşer (mevcut davranış;
  SC-006).
- `customer` rollü kullanıcı bu istemciyle login olur → token'da yönetim scope'u YOK → admin tool 403.
