# Contract: Downstream Registry (tool-adı → BC + yüzey)

Fasadın hangi BC'leri topladığı + tool sahipliği + yüzey eşlemesi. Kaynak: `FacadeOption` config +
oturum-anı ListTools toplaması.

## Config (FacadeOption.Downstreams)

Her downstream:

```
{ Name, McpUrl, Surface: customer|admin, RequiresUserAuth: bool }
```

MVP müşteri yüzeyi (Surface=customer):

| BC | McpUrl (service discovery) | RequiresUserAuth |
|---|---|---|
| storefront | http://storefront-api/mcp | false (anonim keşif/arama) |
| catalog | http://catalog-api/mcp | false (anonim) |
| basket | http://basket-api/mcp | false (anonim sepet; X-User-Key) → login'de devir |
| order | http://order-api/mcp | true (sipariş → step-up login) |
| customer | http://customer-api/mcp | true (adres/hesap) |
| payment | http://payment-api/mcp | true (ödeme bağlamı) |

Yönetim yüzeyi (Surface=admin): catalog/stock/customer `/mcp-admin` (070) — mevcut admin tool setleri.

## Türetilen ToolRoutingRegistry

ListTools toplaması sonrası: her toplanan tool adı → sahibi downstream + yüzey. Kural:

- Tool adları global benzersiz (`Shared/McpToolNames`) → namespace yok.
- Aynı ad iki BC'den gelirse: deterministik ilk-sahip + log (beklenmez).
- `Resolve(name) → (OwnerBc, Surface)`; yoksa null → çağrı reddi.

## Yüzey süzme

- `/mcp` → yalnız `customer`; `/mcp-admin` → yalnız `admin`.
- `RequiresUserAuth` alanı, step-up login'in HANGİ tool'da tetikleneceğini belirler (false=anonim geçer,
  true=401 challenge).