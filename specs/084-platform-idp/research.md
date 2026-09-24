# Research — Platform IdP Terfisi (Dilim A)

## D1 — Uygulama-kayıt modeli (app-registry)

**Karar:** Kayıtlı uygulamalar + scope ad-uzayı + client demetleri **config güdümlü** olur
(`AppRegistryOptions` POCO, `AddOptions().BindConfiguration().ValidateOnStart()`). `Config.cs`'teki
sabit scope/audience/client dizileri bu registry'den beslenir; ECommerce config'te "app #1" olarak durur.

**Gerekçe:** İkinci uygulama (PG) kod değişmeden yalnız config'le tanıtılabilmeli (SC-002). Bugün scope
adları İKİ yerde sabit (`Config.cs` audience + `Common/AuthorizationScopes.cs`) — registry tek giriş yapar.

**Alternatif (red):** DB'de app tablosu — fazladan CRUD/UI; config yeter (app sayısı az, dağıtım-zamanı bilinir).

## D2 — Scope ad-uzayı stratejisi

**Karar:** ECommerce'in MEVCUT BC-seviyeli scope'ları (`catalog.write`, `basket.read`, …) olduğu gibi
kalır = **ECommerce'in ad-uzayı** (toplu). Yeni her uygulama KENDİ tekil üst-prefix'ini sahiplenir
(`pg.*`). Registry açılışta iki app aynı scope adını isterse REDDEDER (SC-004).

**Gerekçe:** Mevcut scope'ları `ecommerce.catalog.write`'a topluca yeniden adlandırmak her servisin
`[RequiredScope]`'unu + config'ini kırar — devasa churn, bugün sıfır değer. Çakışmasızlık, "her app tekil
prefix" kuralıyla renmesiz sağlanır. FR-003 "app ad-uzayı" = uygulama başına ayrık küme (illa `<app>.` literal değil).

**Alternatif (red):** Topyekûn `<app>.<bc>.<perm>` yeniden adlandırma — B/C'yi açmayan saf maliyet.

## D3 — Common bölünmesi + paket

**Karar:** `Common`'ın **nötr auth kablosu** yeni **`Platform.Auth`** paketine çıkar: `IdentityOption`,
`AuthenticationExtension` (`AddAuthenticationAndAuthorizationExtension`). ECommerce + AgentPlatform ikisi de
tüketir. **ECommerce-özel** `AuthorizationScopes` sabitleri ECommerce'te KALIR (app #1'in ad-uzayı). Paket
sürümü `Directory.Packages.props`'ta pinlenir (CPM). **NOT:** `ScopeClaimArrayHandler` NÖTR DEĞİL —
`IOpenIddictServerHandler` (token üretimi, sunucu-tarafı), IdP'ye ait; pakete GİRMEZ, AgentPlatform'a IdP ile taşınır.

**Gerekçe:** IdP taşınınca hem sunucu (IdP) hem relying party'ler (ECommerce servisleri) aynı nötr
kabloya muhtaç → gerçek paylaşım. Scope sabitleri nötr değil, paketle taşınmaz.

**Paket beslemesi:** Dev'de GitHub Packages (private) — `gh` mevcut, tek sahip. [research aşaması: yerel
feed de yeter; karar tasks'ta somutlanır.]

**Alternatif (red):** Common'ı olduğu gibi ikinci repoya kopyalamak — scope sabitleri de sızar, iki kopya drift.

## D4 — AppHost çapraz-repo kablosu

**Karar:** `AgentPlatform` **kendi Aspire AppHost'unu** taşır (idp + `identityDb`). ECommerce AppHost
IdP'yi artık proje-ref (`AddProject<Projects.Identity_Server>`) ile DEĞİL, **dış-servis referansı**
(config/service-discovery URL) ile bulur. Dev'de iki AppHost koşar; ECommerce, AgentPlatform'un issuer
URL'ine bakar (`IdentityOption.Address`).

**Gerekçe:** Aspire proje-ref aynı çözümü ister; ayrı repo → proje-ref imkânsız. Dış-servis referansı
mevcut `IdentityOption` mekanizmasıyla zaten uyumlu (authority config'ten okunuyor).

## D5 — Issuer + HTTPS + loopback

**Karar:** Issuer URL `Program.cs`'te sabit yerine config'ten okunur (dağıtım-nötr); HTTPS zorunlu kalır.
Mevcut loopback muafiyeti (`AdminAgentApplicationManager`, desktop Claude için) OLDUĞU GİBİ kalır —
YENİ loopback-only sert bağımlılık eklenmez (FR-006). Mobil/public-callback inşası bu dilimde YOK.

**Gerekçe:** Taşınınca issuer artık "localhost:5001" varsayamaz; config'e alınır. Var olan loopback
desktop akışını bozmadan durur; mobil kapısı kapanmaz (sadece açılmaz).

## Çözülen NEEDS CLARIFICATION
- Repo extraction A'ya dahil mi → **evet** (kullanıcı); repo adı `AgentPlatform`; Common → paylaşılan paket.