# Data Model: OpenIddict Migrasyonu

**Date**: 2026-08-06 | **Plan**: [plan.md](plan.md)

Domain modeli yok; değişim yalnız `identityDb` altyapı tablolarında.

## Kalan tablolar (dokunulmaz)

- ASP.NET Identity: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`,
  `AspNetUserTokens`, `AspNetRoleClaims` — kullanıcı verisi ve şifre hash'leri aynen (FR-001).
- UserKey altyapısı (004): `ApiKeys`, `UserScopes` — değişmez.

## Eklenen tablolar (OpenIddict EF, yeni migration)

- `OpenIddictApplications` — client kayıtları (seed ile dolar: ecommerce.bff, order-saga, apikeys.admin).
- `OpenIddictScopes` — 13 scope kaydı + her scope'un audience (resource) eşlemesi (seed ile dolar).
- `OpenIddictAuthorizations` — kullanıcı bazlı yetkilendirme kayıtları (runtime'da oluşur).
- `OpenIddictTokens` — refresh token / authorization code kayıtları (runtime'da oluşur; Duende PersistedGrants'ın karşılığı).

## Silinen tablolar / dosyalar

- DB sıfırlanır (Docker volume reset) — drop/veri-taşıma migration'ı YAZILMAZ. Mevcut TÜM migration'lar silinir,
  temiz tek "Initial" migration üretilir (Identity + ApiKeys + OpenIddict tabloları birlikte).
- `PersistedGrantDbContext` + `Data/Migrations/` klasörünün tamamı kaldırılır.
- `Identity.Server/keys/` klasörü (Duende otomatik imza anahtarı dosyası).

## Seed modeli (kod sabitleri, DB'ye açılışta idempotent yazılır)

- **Client**: ClientId, ClientSecret (düz değer bugünkiyle aynı; store hash'ler), grant izinleri, redirect URI'lar,
  scope izinleri, consent tipi (Implicit).
- **Scope→Audience haritası** (Duende ApiResources'un karşılığı, 8 kayıt):
  catalog.write→catalog.api; basket.read/write→basket.api; order.read/write→order.api; payment.read/write→payment.api;
  stock.write+stock.reserve→stock.api; file.write→file.api; storefront.read→storefront.api;
  customer.read/write→customer.api; apikeys.manage→(audience'sız — Identity.Server kendi doğrular, ValidateAudience=false).

## State transitions

Yok — kullanıcı/oturum yaşam döngüsü ASP.NET Identity + OpenIddict kütüphane davranışıdır, özel durum makinesi kurulmaz.