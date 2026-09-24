# Data Model — Platform IdP Terfisi (Dilim A)

Yeni domain aggregate/tablo YOK. Şema (identityDb) taşınır, değişmez. Yeni olan = **config-seviyesi**
kayıt modeli (DB'de değil, `AppRegistryOptions`).

## Config varlıkları (yeni, DB dışı)

### RegisteredApp
Platform kimlik makamına güvenen uygulama.
- `AppId` (string) — tekil uygulama kimliği (ör. `ecommerce`, `pg`)
- `ScopeNamespace` (ScopeNamespace) — bu app'in sahip olduğu yetki kümesi
- `ClientBundles` (list) — bu app için seed edilecek OAuth client'lar + scope grantları
- **Kural:** iki RegisteredApp scope kümesi kesişemez (açılışta doğrulanır → çakışma RET)

### ScopeNamespace
- `Prefix` (string) — app'in üst-prefix'i (yeni app'ler için `pg`, `x`); ECommerce = mevcut BC scope kümesi
- `Scopes` (list<string>) — scope adları (+ audience eşlemesi)
- **Kural:** tüm RegisteredApp scope'larının birleşimi = `KnownScopes.All` (tek-kaynak)

## Mevcut varlıklar (şema değişmez, taşınır)

- **IdentityUser / Role** — ASP.NET Identity; roller `admin`, `customer` seed.
- **RoleScope** (junction: RoleId + Scope) — rol=scope demeti; downstream scope görür (rol değil).
- **OpenIddict Application / Scope / Authorization / Token** — OAuth istemcileri, scope kayıtları, DCR
  ile üretilen dış-agent client'ları, consent (Authorization), token'lar.

## İlişkiler
- RegisteredApp 1—1 ScopeNamespace; RegisteredApp 1—N ClientBundle.
- ScopeNamespace.Scopes → OpenIddict Scope kayıtları (seed) → Role.RoleScope (yetkilendirme).
- Seed akışı (`SeedHostedService`) artık RegisteredApp listesi üzerinden döner (app başına scope + client).