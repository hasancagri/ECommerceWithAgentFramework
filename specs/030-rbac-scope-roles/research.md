# Research: RBAC — Rol = Scope Demeti

**Feature**: 030-rbac-scope-roles | **Date**: 2026-08-06

Kod haritası (Identity.Server, EF Core + OpenIddict + ASP.NET Identity) üzerine oturan
üç kritik tasarım kararı. Kalan tüm noktalar spec varsayımlarıyla çözülü.

## D1 — Scope'lar token verme anında rol demetiyle FİLTRELENİR

**Karar**: Access token basılırken granted API scope'ları = `request scopes ∩ rol demeti`.
Kimlik scope'ları (openid/profile/email/roles/offline_access) her zaman geçer. Uygulama
noktaları: authorization_code için `AuthorizeEndpoint.cs` `SetScopes(...)`; refresh için
`TokenEndpoint.cs` refresh dalı (rol değişimini yansıtmak için scope'lar yeniden türetilir).

**Gerekçe**: Bugün `SetScopes(request.GetScopes())` BFF ne isterse veriyor — her kullanıcı
tüm BFF scope'larını alıyor. Rolü bir "grant filtresi" yapmak OAuth semantiğiyle birebir
uyumlu (granted ⊆ requested) ve downstream'i hiç değiştirmez. Scope array'i
`ScopeClaimArrayHandler` ile aynen kurulmaya devam eder.

**Alternatifler**:
- *Rol claim'ini token'a basıp serviste kontrol* — REDDEDİLDİ (İlke V: downstream rol görmez).
- *BFF'in istediği scope'u role göre değiştirmek* — REDDEDİLDİ (yetki kararı IdP'de olmalı,
  istemcide değil; BFF union ister, IdP keser).

**Refresh notu**: authorization_code'da scope kod içine gömülür; refresh'te principal yeniden
kullanılır. Rol değişiminin "sonraki token"da yansıması için (FR-012) refresh dalında scope'lar
kullanıcının GÜNCEL rolünden yeniden türetilir; erişim token ömrü kısa (D3) olduğundan pencere dar.

## D2 — KnownScopes = kod-sahipli kapalı registry (açıklamalı)

**Karar**: Identity.Server'da atanabilir scope'ların tek listesi — scope adı + insan-okur
açıklama. Kaynak, servislerin scope sabitleridir (`Common/.../AuthorizationScopes.cs`,
bugün `Config.AllApiScopes` olarak da IdP'de mevcut). Bu liste: (a) rol yönetim ekranının
checkbox kaynağı, (b) rol→scope kaydında doğrulayıcı (listede yoksa reddet), (c) seed'in
map kaynağı. Yeni scope `identity.roles.manage` eklenir (rol yönetim yüzeyi için).

**Gerekçe**: Uyumsuzluk (typo/uydurma scope) tek yerde, giriş anında engellenir (FR-006).
Scope deploy-zamanı sabittir; runtime discovery gereksiz.

**Alternatifler**:
- *Serbest metin scope girişi* — REDDEDİLDİ (uyumsuzluk riski, feature'ın var oluş sebebi).
- *Servislerin açılışta scope publish etmesi (dinamik registry)* — REDDEDİLDİ (over-engineering;
  scope statik). Bedel kabul: yeni scope eklenince IdP registry'sine de eklenir (iki yer).

## D3 — Rol yönetim yüzeyi Identity.Server Razor Pages'te, admin-rol cookie ile korunur

**Karar**: Rol CRUD, rol→scope işaretleme ve kullanıcı-rol atama ekranları Identity.Server'ın
kendi Razor Pages'inde (`Pages/Admin/*`) yaşar. Bu sayfalar, giriş yapmış cookie kullanıcısının
`admin` rolünde olmasını ister. Eşdeğer `identity.roles.manage` scope'u API/programatik erişim
için tanımlı kalır ve admin rol demetindedir.

**Gerekçe**: Rol yönetimi rol otoritesinin (IdP) kendi iç yüzeyidir; IdP kendi admin UI'ını
rolle koruyabilir — İlke V'in "downstream servis rol görmez" kuralı IdP'nin KENDİSİNİ kapsamaz
(IdP downstream değildir, rol otoritesidir). Cookie-tabanlı server-rendered sayfalarda JWT scope
guard'ı doğal oturmaz; cookie principal'ındaki rol doğal guard'dır.

**Not (İlke V ile hizalama)**: Anayasa "back-office dahil her yüzey scope ile korunur" der.
Buradaki incelik: downstream servisler için geçerli; IdP'nin kendi yönetim UI'ı rol otoritesinin
iç yüzeyi olduğundan cookie-rol guard'ı meşrudur. `identity.roles.manage` scope'u yine tanımlı ve
admin demetinde — herhangi bir API varyantı onu kullanır. Bu, plan.md Constitution Check'te açık
işaretlenir; gerekirse anayasaya küçük bir açıklayıcı not eklenebilir (amendment gerekmez).

**Alternatifler**:
- *Admin UI'ı WebApp storefront'a koymak* — REDDEDİLDİ (storefront BFF'i; admin/kullanıcı-rol
  yönetimi oraya ait değil, IdP'ye ait).

## D4 — Tek-rol kısıtı ASP.NET Identity üstünde uygulama katmanında

**Karar**: AspNetUserRoles çok-çok'tur; tek-rol kuralı (FR-001) uygulama katmanında zorlanır —
rol atama daima önce mevcut rol(ler)i kaldırır sonra yenisini ekler. Kullanıcı asla rolsüz
kalmaz (register→customer, atama→değiştir).

**Gerekçe**: Şema değişikliği gerektirmez; Identity'nin hazır UserManager API'siyle (`GetRolesAsync`,
`RemoveFromRolesAsync`, `AddToRoleAsync`) yürür.

**Alternatifler**:
- *ApplicationUser'a tekil RoleId kolonu* — REDDEDİLDİ (Identity'nin rol altyapısını atlar,
  RoleManager/rol tablolarıyla tutarsızlık).

## D5 — Bootstrap admin + seed genişletmesi

**Karar**: `SeedHostedService` genişletilir: (1) `admin`+`customer` rolleri (RoleManager,
idempotent), (2) rol→scope map (RoleScope tablosu, KnownScopes'tan), (3) rolü admin olan
bootstrap admin kullanıcı (email+parola config'ten), (4) ingestion-agent client (client_credentials,
catalog.write+stock.write). order-saga zaten var. (5) mevcut rolsüz kullanıcı backfill→customer.

**Gerekçe**: Tavuk-yumurta (FR-016/019); mevcut seed deseni idempotent ve boot'ta koşuyor.

**Bootstrap admin scope demeti (seed varsayılanı)**:
- `customer`: basket.write, order.create, order.read, stock.reserve, storefront.read,
  customer.* (wallet/adres) — kısaca BFF kullanıcı scope'larından yönetim-dışı olanlar.
- `admin`: customer demetinin tümü + catalog.write, feed.manage, apikeys.manage,
  identity.roles.manage (ve yönetim gerektiren diğer yazma scope'ları).
- Kesin liste data-model + implementasyonda `AuthorizationScopes`/`Config.AllApiScopes`'tan
  türetilir; catalog okuma anonim olduğu için scope'a gerek yok.

## D6 — Giriş noktası: WebApp header'ında koşullu "Yönetim" linki

**Karar**: Rol yönetim ekranına giriş, WebApp storefront header'ındaki koşullu bir "Yönetim"
linkiyle olur. Link yalnız kullanıcının token'ında `identity.roles.manage` scope'u varsa
görünür ve IdP'nin `/Admin/Roles` sayfasını (IdP origin, `localhost:5001`) açar. OIDC login
sonrası IdP'de zaten SSO cookie'si olduğundan sayfa doğrudan açılır.

**Gerekçe**: İnsan WebApp'te gezinir; IdP'nin storefront-nav'ı yok. Ekranı IdP'de tutup
(D3) yalnız girişi WebApp'ten vermek en az parça. Link görünürlüğü `identity.roles.manage`
scope'una bağlıdır — bu **kozmetiktir** (gerçek kapı IdP sayfasında), scope'a bakmak rol
claim'ine bakmaktan İlke V açısından daha temiz.

**Kapsam etkisi**: Tek küçük WebApp değişikliği — header'a scope-koşullu link. Yetki kararı
değil; downstream rol kullanımı yok.

**Alternatifler**: IdP landing sayfası / elle URL — REDDEDİLDİ (keşfedilebilir değil).

## Çözülü varsayımlar (araştırma gerektirmeyen)

- **Persistence**: Identity.Server EF Core + Postgres (identityDb). RoleScope yeni EF entity +
  migration. AspNetRoles/AspNetUserRoles zaten var.
- **Access token ömrü (D3 penceresi)**: kısa (dakikalar) + refresh; anlık revocation kapsam dışı.
- **Domain-TDD kapsamı (İlke VI)**: saf birimler test-first — (a) scope çözümleme
  (`granted = requested ∩ roleBundle`), (b) KnownScopes doğrulama (bilinmeyen scope reddi),
  (c) tek-rol kuralı (atama mevcut rolü değiştirir). Bunlar mock'suz test edilir.