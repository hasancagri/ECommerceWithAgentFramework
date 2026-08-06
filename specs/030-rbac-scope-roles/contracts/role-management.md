# Contract: Rol Yönetim Yüzeyi + Token Scope Davranışı

**Feature**: 030-rbac-scope-roles | **Host**: Identity.Server

İki kontrat: (1) admin rol yönetim yüzeyi (Razor Pages handler'ları), (2) token verme
davranışı (scope filtreleme). Identity.Server IdP olduğundan REST-CRUD servis kontratı
değil, sayfa-handler + token-endpoint davranış kontratıdır.

## 0. Giriş Noktası (WebApp header, D6)
- WebApp storefront header'ında "Yönetim" linki — yalnız token'ında `identity.roles.manage`
  scope'u olan kullanıcıya görünür (kozmetik görünürlük, yetki değil).
- Link IdP'nin `/Admin/Roles` sayfasını açar (IdP origin); SSO cookie'siyle giriş hazırdır.

## 1. Rol Yönetim Yüzeyi (`Pages/Admin/*`, cookie auth, admin rolü zorunlu)

Tümü giriş yapmış **admin** rolündeki cookie kullanıcısını ister (D3). Admin değilse 403/
yönlendirme. `identity.roles.manage` scope'u eşdeğer programatik guard olarak tanımlıdır.

### Roller
- **Rolleri listele** — tüm roller + her rolün scope sayısı.
- **Rol yarat** — girdi: benzersiz ad. Ad çakışırsa hata. Yeni rol boş scope demetiyle başlar.
- **Rol sil** — seed rolü (`admin`/`customer`) veya kullanıcısı olan rol reddedilir (INV-5).

### Rol → Scope
- **Rol scope'larını getir** — rolün mevcut işaretli scope'ları + tüm KnownScopes (açıklamalı).
- **Rol scope'larını kaydet** — girdi: seçili scope adları (checkbox). Her scope KnownScopes'ta
  OLMALI (INV-1); değilse tüm işlem reddedilir. Kaydedince RoleScopes rolü için tam olarak
  bu kümeye set edilir.

### Kullanıcı → Rol
- **Kullanıcıları listele** — kullanıcı + mevcut (tek) rolü.
- **Kullanıcı rolünü belirle** — girdi: kullanıcı + hedef rol (ikisi de mevcut listeden).
  Mevcut rolü kaldırır, hedefi ekler (INV-2/3). Son admin'in rolünü değiştirmek reddedilir (INV-4).

### KnownScopes
- **Bilinen scope'ları listele** — kod registry'sinden ad + açıklama. Salt-okunur; ekran
  buradan checkbox doldurur. Serbest metin girişi YOK (FR-006).

## 2. Token Verme Davranışı (mevcut endpoint'ler, davranış değişir)

### authorization_code (`/connect/authorize` → `/connect/token`)
- **Önce**: granted API scope'ları = request'in istediği scope'lar (rol süzgeci yok).
- **Sonra**: granted API scope'ları = `request scopes ∩ kullanıcının rol demeti` (D1).
  Kimlik scope'ları (openid/profile/email/roles/offline_access) her zaman geçer.
- access_token `scope` claim'i `ScopeClaimArrayHandler` ile dizi olarak kurulur (DEĞİŞMEZ).

### refresh (`/connect/token` grant_type=refresh_token)
- granted API scope'ları kullanıcının GÜNCEL rol demetiyle yeniden süzülür (FR-012):
  rol değişimi bir sonraki (refresh) token'da yansır.

### client_credentials (makine, `order-saga`/`ingestion-agent`)
- DEĞİŞMEZ: scope client'ın statik grant'ından gelir; rol süzgeci UYGULANMAZ (RBAC dışı).

## 3. Register Davranışı (`Pages/Account/Create`, davranış eklenir)
- Yeni kullanıcı oluşturulunca otomatik `customer` rolü atanır (FR-013); form rol seçmez.
- Aktivasyon/onay adımı YOK; kullanıcı doğrudan login olur (FR-014).

## 4. Seed Davranışı (`SeedHostedService`, genişler — idempotent)
- Roller: `admin`, `customer` (yoksa yarat).
- RoleScopes: seed map (D5) — customer/admin demetleri KnownScopes'tan.
- Bootstrap admin: rolü admin olan kullanıcı; email+parola config'ten (kodda parola yok).
- Client: `ingestion-agent` (client_credentials, catalog.write+stock.write) yoksa yarat.
- Backfill: rolsüz mevcut kullanıcılar → `customer` (FR-021).

## Hata / Kenar Davranışları
- Bilinmeyen scope kaydı → tüm kaydet reddedilir (kısmi yazım yok).
- Seed/kullanıcılı rol silme → reddedilir.
- Son admin rol değişimi → reddedilir.
- Rol değişimi eldeki access token'ı ETKİLEMEZ; sonraki token'da yansır.