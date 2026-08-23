# Identity.Server — Domain Süreci

**BC ne yapar:** Sistemin tek kimlik sağlayıcısıdır (OpenIddict + ASP.NET Identity). Kullanıcıyı
doğrular, OIDC/OAuth token verir; verme anında **rolü scope demetine açar** — downstream servisler
yalnız scope görür, rolü asla görmez.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kullanıcı kayıt olur.** WebApp `prompt=create` gönderir →           `(AuthorizeEndpoint`
   kayıt sayfasına yönlenir; create prompt returnUrl'den temizlenir.     ` → Create.Index)`
2. **Yeni kullanıcı otomatik `customer` rolü alır** (sunucu atar,        `(RoleAssignmentService`
   seçilemez); ardından doğrudan login (aktivasyon-mail yok).            ` .CustomerRole)`
3. **Kullanıcı parolayla doğrulanır.** ASP.NET Identity cookie'si        `(Login.Index →`
   kurulur; kilit/2FA nedeni ayrıştırılır.                               ` PasswordSignInAsync)`
4. **Authorize cookie'yi kimlik claim'lerine çevirir.** sub/name/email   `(AuthorizeEndpoint)`
   biner; rol yalnız id_token'a düşer (UI kararı için), access token'a değil.
5. **Rol scope demetine AÇILIR.** Kullanıcının rolleri rol→scope         `(RoleScopeQuery`
   map'inden okunup birleşik demet çözülür.                             ` .GetUserScopeBundleAsync)`
6. **Verilen scope = talep ∩ (rol demeti ∪ kimlik scope'ları).**        `(ScopeResolver.Resolve)`
   Demette olmayan / kapalı registry'den düşmüş scope token'a YAZILMAZ.
7. **Token verilir; kaynak (`aud`) scope'lardan üretilir.** Rol          `(TokenEndpoint)`
   claim'i access token'a girmez → downstream yalnız scope doğrular.
8. **Refresh'te scope GÜNCEL rol demetiyle yeniden süzülür.** Rol        `(TokenEndpoint`
   düşürülmüşse yetki bir sonraki token'da daralır (FR-012).             ` refresh_token)`
9. **M2M (client_credentials): sub = client id.** Talep edilen scope     `(TokenEndpoint`
   doğrudan biner (rol yok); order-saga/apikeys için servis token'ı.     ` client_credentials)`
10. **Admin yönetir.** `/Admin/*` (cookie + admin rolü) roller ile       `(Admin.Roles.Scopes`
    rol→scope map'ini düzenler; checkbox kaynağı kapalı registry.         ` → SetRoleScopesAsync)`

## Domain kuralları (süreci yöneten değişmezler)

- **Rol = scope demeti (İLKE V).** Rol yalnız token verme anında scope'a açılır; token'a rol yazılmaz.
- **Rol BC sınırını geçmez.** Downstream rolü hiç görmez, yalnız scope ile yetki kararı verir.
- **KnownScopes KAPALI registry.** Atanabilir scope tek kaynaktan (`Config.AllApiScopes`); DB/ekran yeni
  scope üretemez — rol→scope yazımı `AssignableScopeValidator` ile bilinmeyeni reddeder (INV-1).
- **Register → `customer`, tek rol.** Sunucu atar; son admin'in rolü değiştirilemez, seed rolü silinemez.
- **Seed idempotent, ekran ezilmez.** Rol→scope map yalnız YOKken doldurulur (`SeedHostedService`);
  admin düzenlemesi yeniden başlatmada korunur.
- **HTTPS zorunlu.** IdP Secure-cookie ister; HTTP'de login döngüye girer (issuer = tüm servislerin adresi).

## Sınır (bu BC'nin dokunmadığı)

Sepet/sipariş/ödeme iş mantığı yok. Kullanıcı verisi (cüzdan/adres) Customer BC'de; bu BC yalnız kimlik
+ yetki üretir. Downstream'de scope doğrulama her servisin kendi middleware'inde (`UserInfoEndpoint` +
JWT bearer), rol genişletme burada biter.
