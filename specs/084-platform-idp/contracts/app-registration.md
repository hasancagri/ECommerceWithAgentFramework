# Contract — Uygulama-Kayıt Config Sözleşmesi

Platform kimlik makamına bir uygulamanın nasıl **config'le** kaydolduğu. `AppRegistryOptions` POCO'suna
bağlanır (`AddOptions<AppRegistryOptions>().BindConfiguration("AppRegistry").ValidateOnStart()`).
`IConfiguration` doğrudan okunmaz.

## Şekil (kavramsal)

```
AppRegistry:
  Apps:
    - AppId: <tekil app kimliği>            # ör. "ecommerce", "pg"
      ScopeNamespace:
        Prefix: <üst-prefix>                # yeni app'ler için "pg"; ECommerce = mevcut BC kümesi
        Scopes:
          - Name: <scope adı>               # ör. "catalog.write" | "pg.commission.admin"
            Audience: <api audience>         # ör. "catalog.api" | "pg.api"
      Clients:                               # bu app için seed edilecek OAuth client demetleri
        - ClientId: <client id>
          Type: public-pkce | m2m
          Scopes: [<scope adı>, ...]
```

## Değişmezler (açılışta doğrulanır — hata = boot RET)

1. **AppId tekil** — iki app aynı `AppId`'yi alamaz.
2. **Scope kesişmezliği** — iki farklı app'in `Scopes` kümesi kesişemez (SC-004; FR-003).
3. **Client scope alt-küme** — bir client yalnız KENDİ app'inin scope'larını grant edebilir.
4. **KnownScopes tek-kaynak** — `KnownScopes.All` = tüm app'lerin scope birleşimi; başka kaynaktan scope üretilmez.
5. **Downstream sözleşmesi** — token yalnız scope taşır; rol adı token'da yetki kaynağı DEĞİL (RBAC 030).

## ECommerce (app #1) kaydı — örnek doğrulama girdisi

- `AppId: ecommerce`; ScopeNamespace = mevcut `AuthorizationScopes` kümesi (catalog.*, basket.*, order.*,
  payment.*, stock.write, customer.*, reviews.write, library.*, discount.*, storefront.read).
- Clients: `external-customer-agent` (public-pkce, müşteri scope demeti), `external-admin-agent`
  (public-pkce, admin scope demeti), m2m client'lar (order-saga, payment-s2s, mcp-gateway-discovery).

## İkinci-app tanıma testi (SC-002/SC-004)

- `AppId: pg`, Prefix `pg`, Scopes `[pg.merchant, pg.commission.admin]` config'e EKLENİR → makam açılır,
  ad-uzayı tanınır, ECommerce kodu değişmez.
- `pg` scope'una `catalog.write` (ECommerce'e ait) eklenirse → açılış REDDEDER (kesişim ihlali).