# Research: 085 Tek MCP Yüzeyi

Kod keşfi 2026-09-26 (fasat + 4 BC + gateway + AgentPlatform tarandı). Spec'in plan'a bıraktığı 4 açık + keşfin çıkardığı 2 yeni karar:

## R1 — Budama NEREDE koşar: BC oturum-açılışında (scope-bazlı ConfigureSessionOptions)

**Decision**: Tek gerçek-kaynak BC'dir. Her BC'nin `ConfigureSessionOptions`'ı yol-prefix yerine oturumu açan token'ın scope claim'lerine bakar: `*AdminSurface.ToolNames` içindeki tool, eşlenen scope token'da varsa listede kalır; yoksa çıkar. Token'sız oturum = yalnız anonim set (Discount'ta boş). Fasat AYRICA tool→scope bilgisi tutmaz.

**Rationale**: Bugünkü mekanizmanın (070) en küçük evrimi — aynı hook, aynı holder dosyaları, sinyal değişir. Tool→scope eşlemesi zaten BC'de yaşıyor (`[RequiredScope]` + `*AdminSurface`); fasada kopyalamak drift üretir (074 allowlist tuzağının fasat kopyası olurdu).

**Alternatives considered**: (a) Fasatta merkezi tool→scope haritası — çift kaynak, drift; (b) tool adı prefix konvansiyonu (`admin_*`) — 074'te bilinçle reddedilen ad-prefix filtresine geri dönüş.

## R2 — Fasat tools/list: scope-parmakizi anahtarlı cache + kullanıcı token'ıyla toplama

**Decision**: `ToolCatalogCollector.GetAsync` anahtarı yüzey değil **scope-parmakizi** olur (token'daki sıralı scope kümesinin hash'i; token'sız = "anon"). Toplama, oturum sahibinin TOKEN'ıyla yapılır — BC zaten scope'a göre budanmış liste döner, fasat süzme yapmaz. Yönlendirme registry'si (ad→BC) m2m token'lı TAM katalog taramasından ayrı cache'lenir (`DiscoveryTokenSource` mevcut scope listesiyle tüm admin tool'larını görür — appsettings'te hazır).

**Rationale**: Aynı scope setine sahip herkes cache'i paylaşır (pratikte 2-3 kayıt: müşteri seti, admin seti, anon) — bugünkü 2-kayıtlı cache maliyetiyle eşdeğer. Fasat "taşıyıcı" kimliğini korur: yetki bilgisi fasada sızmaz (İLKE V ruhu). Bilinmeyen ada çağrı BC'de `[RequiredScope]`/404 ile ölür — 403 son savunma kabulü spec'te.

**Alternatives considered**: (a) m2m tam liste + fasatta scope süzme — tool→scope haritası fasada sızar (R1'le çelişir); (b) kullanıcı başına cache — gereksiz kardinalite; (c) cache'siz her seferinde toplama — N BC × her tools/list, pahalı.

## R3 — IdP: istemci-tavanı OpenIddict ön-validasyonundan ScopeResolver kesişimine taşınır (AgentPlatform, AYRI PR)

**Decision**: `ScopeResolver.Resolve` bugün `talep ∩ (rol ∪ her-zaman-izinli)` — istemci tavanı OpenIddict'in scope-izin ÖN-validasyonunda (izinsiz scope talebi = `invalid_scope` RED). Union PRM'de müşteri istemcisi admin scope'larını da talep edeceğinden bu red müşteri bağlantısını kırar (FR-006 ihlali). Çözüm: OpenIddict scope-izin ön-validasyonu gevşetilir (`IgnoreScopePermissions`), tavan `ScopeResolver`'a 4. parametre olarak girer: `granted = talep ∩ istemciTavanı ∩ (rol ∪ her-zaman-izinli)`. Tavan, application kaydının izinli scope'larından okunur.

**Rationale**: Çift duvar (istemci tavanı + rol) AYNEN kalır — sadece "reddet" yerine "ele" davranışına geçer; DCR tavanı (FR-005) istemci kaydının izin setiyle korunmaya devam eder. Saf fonksiyon büyür → İLKE VI test-first tam uyar.

**Alternatives considered**: (a) `IgnoreScopePermissions` + yalnız rol duvarı — DCR istemcisi admin kullanıcı consent'iyle admin scope kapabilir, FR-005 İHLAL, red; (b) mcp-remote'un istemci başına scope talebini kısması — istemci davranışı bizim kontrolümüzde değil; (c) Option C'ye dönüş (işaretli PRM) — clarify'da A kilitlendi.

## R4 — Desktop çift-kayıt cache ayrışması: URL query işareti, sunucu tarafı SIFIR kod

**Decision**: Admin Desktop kaydı URL'ye anlamsız query işareti ekler (ör. `https://host/mcp?client=admin`); mcp-remote cache anahtarını URL'den türettiği için token depoları ayrışır. Sunucu query'yi YOK SAYAR (tek PRM, tek uç — R3/A kararıyla uyumlu). Konvansiyon quickstart'ta belgelenir.

**Rationale**: FR-007'yi sıfır sunucu koduyla sağlar; statik client-info alternatifi mcp-remote bayrak sözdizimine bağımlı ve kullanıcı config'ini şişirir.

**Alternatives considered**: statik OAuth client bilgisi bayrağı (mcp-remote `--static-oauth-client-info`) — çalışır ama kırılgan; ayrı hostname — dev ortamında gereksiz tören.

## R5 — Discount BC: tek ucu `/mcp-admin`'den korumalı `/mcp`'ye taşınır

**Decision**: Discount'un anonim seti yok; `MapMcp("/mcp").RequireAuthorization()` olur, budama R1 kuralıyla (`discount.admin.write` yoksa boş liste). Fasat config'inde `discount` entry'si `McpUrl=/mcp` olur.

**Rationale**: Keşif bulgusu — spec başta 3 BC sayıyordu, gerçek 4 (FR-001a eklendi). Tek istisnası: anonim fallback'i yok.

## R6 — Gateway (YARP) + PRM temizliği

**Decision**: Gateway'deki `/mcp-admin/{service}` rotaları (15 rota bloğunun admin yarısı) ve BC `AddMcpAdminResourceMetadata` kayıtları (slug `mcp-admin/<bc>`) silinir; BC PRM'leri `mcp/<bc>` slug'ında kalır, `scopes_supported` BC'nin tam demetini (anonim+admin) ilan eder. Fasat PRM tek: `/mcp` slug, union demet. `SurfaceFilter` + `DownstreamBc.Surface` alanı + `FacadeScopes.Admin/Customer` ayrımı ölür (tek union sabiti kalır); `SurfaceFilterTests` yerini scope-budama birim testlerine bırakır.

**Rationale**: Ölü yol bırakmama (074 söküm disiplini emsali); PRM = fiilî scope sözleşmesi (070 tuzak notu) — ilan gerçek davranışla eşleşmeli.

## Sıralama notu

AgentPlatform PR'ı (R3) ÖNCE merge edilir; ECommerce tarafı ondan bağımsız çalışır (eski iki-uç düzeni R3'le de çalışıyor), ama union PRM AÇILMADAN önce R3 canlıda olmalı — aksi halde müşteri istemcisi ilk union talepte kırılır.
