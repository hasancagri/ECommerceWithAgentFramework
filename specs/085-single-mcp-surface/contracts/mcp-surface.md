# Kontrat: Tek MCP Yüzeyi (085)

Dış istemcilerin (Claude Desktop/mcp-remote) ve fasadın gördüğü yüzey sözleşmesi. Bu dosya = hedef durum; bugünkü iki-uç düzeni EMEKLİ.

## Endpoint'ler

| Sahip | Uç | Auth | Not |
|---|---|---|---|
| Mcp.Gateway | `POST /mcp` | `RequireAuthorization` (RequireLoginUpfront=true) | TEK dış kapı; müşteri + admin aynı uç |
| Mcp.Gateway | `/.well-known/oauth-protected-resource/mcp` | anonim | TEK PRM |
| Catalog.Api | `POST /mcp` | anonim uç; admin tool'lar scope'la | 070 anonim keşif seti değişmez |
| Stock.Api | `POST /mcp` | anonim uç; admin tool'lar scope'la | " |
| Customer.Api | `POST /mcp` | `RequireAuthorization` | müşteri + merchant-admin tool'ları scope'la ayrışır |
| Discount.Api | `POST /mcp` | `RequireAuthorization` | yalnız admin seti; scope yoksa boş liste |
| — | `POST /mcp-admin` (her yerde) | — | **404 — uç yok** (fasat, 4 BC, gateway rotaları, PRM'ler) |

## PRM (RFC 9728) — fasat

```json
{
  "resource": "<base>/mcp",
  "authorization_servers": ["<idp-issuer>/"],
  "scopes_supported": [ <müşteri demeti> + <admin demeti> ],   // UNION (clarify A)
  "bearer_methods_supported": ["header"]
}
```

- **Union'ın gerçek-kaynağı seed istemci izin setleridir** (analiz C1): müşteri demeti = `external-customer-agent` izinleri (basket/order/customer/payment/storefront + `reviews.write` + `library.read` + `library.write`); admin demeti = `external-admin-agent` izinleri (`catalog.read`, `catalog.write`, `stock.write`, `merchant.credentials.write`, `discount.admin.write`). 079 tuzağı: PRM'de ilan edilmeyen scope istenmez → token audience taşımaz → 401.
- 401 challenge `scope=` listesi PRM ile birebir aynı union.
- BC PRM'leri (`mcp/<bc>` slug): kendi TAM demetini ilan eder — customer `merchant.credentials.write` dahil; Discount `/mcp` kendi PRM'ini açar.

## Oturum tool-seti kuralı (BC ConfigureSessionOptions)

```
tool ∈ liste  ⟺  tool ∉ AdminSurface.ToolNames            (anonim/temel set)
              ∨  requiredScope(tool) ∈ token.scopes        (scope başına, hep-ya-hiç DEĞİL)
```

- `requiredScope` eşlemesi BC'nin kendi `*AdminSurface` holder'ında (tek kaynak; fasada kopyalanmaz).
- Token'sız oturum: yalnız anonim set (Discount: boş).
- Kısmi admin (ör. yalnız `catalog.read`): o scope'un tool'ları görünür, kalanlar görünmez.

## Fasat davranışı

- `tools/list`: oturum token'ı downstream keşfine AYNEN taşınır; cache anahtarı token scope-parmakizi (token'sız = `anon`).
- `tools/call`: değişmez — ad→BC registry (m2m tam-katalog taramasından), kullanıcı Bearer forward, scope denetimi BC'de.
- Listede olmayan/yetkisiz tool çağrısı: BC reddeder → MCP tool-error (403 son savunma, spec kabulü).

## IdP token sözleşmesi (AgentPlatform — ayrı PR)

```
granted = requested ∩ clientCeiling ∩ (roleBundle ∪ alwaysAllow)
```

- Tavan-üstü scope talebi HATA DEĞİL — sessizce elenir (FR-006). OpenIddict scope-izin ön-validasyonu bu akış için gevşetilir; tavan `ScopeResolver`'da zorlanır.
- İstemciler: `external-customer-agent` (müşteri demeti tavanı) + `external-admin-agent` (admin demeti tavanı) — İKİSİ de `/mcp`'ye bağlanır. DCR istemcileri: `ExternalAgentDefaults` tavanı, admin scope'ları giremez (değişmez).

## Claude Desktop kayıt konvansiyonu

```json
"magaza":       { "args": ["mcp-remote", "https://host/mcp"] }
"magaza-admin": { "args": ["mcp-remote", "https://host/mcp?client=admin"] }
```

- `?client=admin` sunucuda YOK SAYILIR; tek işlevi mcp-remote token-cache anahtarını ayrıştırmak (FR-007).
