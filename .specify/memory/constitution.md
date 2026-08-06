<!-- Sync Impact Report — v1.6.0 → v1.7.1 (2026-08-06; v1.7.0 MINOR + v1.7.1 PATCH)
     v1.7.1 PATCH: İlke V'e açıklayıcı istisna — IdP'nin kendi server-rendered admin UI'ı cookie
     ile auth olur (scope yok), admin rolüyle korunur; "back-office scope-gated" downstream API
     içindir. 030-rbac analyze K1'i kapatır. Templates ✅ değişmez.
     --- v1.7.0 MINOR ---
     Modified: İlke V keskinleştirildi. Rol mekanizması netleşti — rol artık token'a "claim
     olarak biner" değil, token verme anında rol→scope map'inden SCOPE DEMETİNE açılır;
     downstream servisler rolü GÖRMEZ (rol claim ile yetki yok, [Authorize(Roles=...)] yok).
     Scope kümesi KOD-sahipli kapalı registry (KnownScopes); rol→scope ve rol yönetimi DB'de,
     admin ekrandan; admin serbest scope string giremez, listeden seçer. Back-office dahil her
     yüzey scope ile korunur (rol-policy downstream'de kullanılmaz). Makine kimlikleri
     (agent/saga) client_credentials + statik scope, RBAC dışı. Register aktivasyon-mail
     ZORUNLU DEĞİL (customer direkt login). Seed: admin+customer rolleri, rol→scope map ve
     login olabilen bootstrap admin kullanıcı. Templates: plan/spec/tasks ✅ değişiklik gerekmez
     (İlke V referansı yok). Runtime docs: CLAUDE.md ⚠ pending — RBAC feature merge olunca
     "rol→scope map" + KnownScopes + ekran akışı hizalanır. -->

# ECommerceWithAgentFramework Constitution

Bu anayasa, projenin pazarlık edilemez mimari ilkelerini tanımlar. Her spec, plan
ve implementation bu ilkelere uymak zorundadır. Ayrıntılı, çalışma-anı (runtime)
rehberi için `CLAUDE.md`'ye bakılır; çatışma halinde bu anayasa üstündür.

## Core Principles

### I. Bounded Context İzolasyonu (NON-NEGOTIABLE)

Her mikroservis bir Bounded Context'tir ve sınırı fiziksel/serttir. Her context'in
kendi Postgres veritabanı, kendi Marten şeması ve kendi domain modeli vardır;
**ortak (paylaşılan) bir domain modeli yoktur**.

- Aynı kavram farklı context'te farklı modeldir (ör. "Ürün" Catalog'da zengin bir
  aggregate, Basket'te sade bir BasketItem entity'si, Storefront'ta bir read-model
  satırıdır). Bir context'in modeli diğerine sızdırılamaz.
- Bir servis başka bir servisin veritabanına, tablosuna, DbContext'ine veya
  aggregate'ine **doğrudan erişemez**.
- Context'ler arası iletişim **integration event'leri** (RabbitMQ fanout), **MCP** ve —
  anlık evet/hayır gerektiren senkron kararlar ile sürecin sahibi BC'de koşan orkestre
  edilmiş saga'ların adım/telafi komutları için — **tipli senkron RPC (gRPC/HTTP)**
  ile olur. Senkron RPC yalnız bilinçli bir kontrat üzerinden yapılır ve DB izolasyonunu
  bozmaz (bir servis diğerinin DB/tablo/aggregate'ine yine doğrudan erişemez). Paylaşılabilen
  tek şey `Shared.IntegrationEvents` / `Shared/Protos` gibi bilinçli sözleşmelerdir.
  Örnekler: stok rezervasyonu (012) Basket/Order→Stock gRPC (request/response zorunlu; async
  event anlık karar veremez); checkout saga adımları (028) Order→Stock/Basket gRPC (hedefli
  komut + telafi, süreç bilgisi yalnız saga'da).

### II. Zengin Aggregate, İçeride Korunan Invariant'lar

Domain modeli anemik olamaz. Her servis tek bir Bounded Context'tir; bir BC, domain'i
gerektiriyorsa **birden fazla zengin aggregate root** içerebilir (ör. Catalog: `Product`,
`Category`, `Brand`). Her aggregate root `AggregateRoot`'tan türer ve kendi tutarlılık
sınırıdır; dış dünya durumu yalnızca kök üzerinden değiştirir.

- Yeni bir aggregate ancak kendi kimliği, kendi invariant'ları ve kendi yaşam döngüsü
  varsa açılır; **anemik (davranışsız) aggregate yasaktır** — davranışsız kavram value
  object ya da entity olarak mevcut bir aggregate'in içinde modellenir.
- Aynı BC içindeki aggregate'ler birbirine nesne referansıyla değil **Id ile** referans
  verir (ör. `Product.BrandId`); bir aggregate diğerinin iç durumunu değiştiremez.

- Koleksiyonlar private tutulur, yalnızca okunur expose edilir (`_items` →
  `IReadOnlyList<T> Items`); mutasyon sadece davranış metotlarından geçer.
- İş kuralı/invariant handler'da değil **aggregate metodunda** korunur. Yeni kural
  eklerken önce aggregate metoduna bakılır.
- Yapı taşları doğru seçilir: kimlik+denetim gereken aggregate-dışı sınıf
  `BaseModel`'den türer; sade entity base almaz; value object'ler `record` +
  private ctor + statik `Create` fabrikasıyla yazılır; enum yerine `Enumeration`.

### III. Vertical Slice + CQRS, Repository Yok

Kod teknik katmana göre değil domain feature'ına göre düzenlenir. Bir feature = bir
`static class`; command/query `record`, `Response`, `Handler` ve endpoint-extension
kendi içine gömülüdür.

- **CQRS ayrımı zorunlu:** durumu değiştiren işlemler `Features/Commands/` altında
  (`IDocumentSession` ile yazar, handler `[Transactional]`), yalnızca veri
  döndürenler `Features/Queries/` altında. İkisi tek slice'ta birleştirilmez.
- **Repository yoktur.** Handler'lar kalıcılık için doğrudan Marten
  `IDocumentSession`'ı, başka bir slice'ı çağırmak için `IMessageBus` alır.
- Endpoint'ler Minimal API'dir; `*EndpointExtension` üzerinden map'lenir, kullanıcı
  `CurrentUser.Load(...)` ile çözülür, `.RequireAuthorization(...)` ile korunur.
- MCP tool'ları ince sarmalayıcıdır: aynı Wolverine command/query'sini `IMessageBus`
  ile yeniden çağırır, iş mantığı eklemez.

### IV. Result Pattern (exception değil)

Beklenen hatalar (bulunamadı, doğrulama, iş kuralı ihlali) exception ile değil bir
**Result nesnesiyle** taşınır. Handler'lar, aggregate metotları ve endpoint'ler her
zaman `Common.Results` altındaki bir Result tipi döner
(`FeatureResultModel`, `FeatureObjectResultModel<T>`, `FeatureListResultModel<T>`,
`FeaturePagedResultModel<T>`, `ResultDomain`).

- Sonuçlar statik fabrikalarla üretilir (`Ok`, `Error`, `NotFound`); `new` ile değil.
- Hata `MessageItem` ile taşınır ve `Code` **serbest metin değil bir resource
  sabitidir**; yeni mesaj eklerken önce resource sabiti tanımlanır.
- Exception yalnızca gerçekten beklenmeyen durumlar içindir; onları
  `GlobalExceptionHandler` yakalar.

### V. Scope-Öncelikli Yetkilendirme + Rol = Scope Demeti

Kimlik merkezi IdP `Identity.Server` tarafından verilir (OpenIddict + ASP.NET Identity).
Servisler JWT bearer doğrular ve **yalnız scope** bazında yetkilendirir
(`AuthorizationScopes.*`). Basım merkezidir, zorlama dağıtıktır; merkezi yetki-karar
servisi (PDP) açılmaz.

- **Rol = token verme anındaki scope demetidir.** Kullanıcının rolleri, token
  basılırken rol→scope map'inden scope'lara **açılır** ve access_token'ın `scope`
  claim'ine yazılır. Downstream servisler rolü **GÖRMEZ**: rol claim'iyle yetki kararı
  verilmez, `[Authorize(Roles=...)]` kullanılmaz. Rol yalnız IdP'de yaşar; BC izolasyonu
  korunur (servis rol taksonomisini bilmez, yalnız scope).
- **Scope kümesi KOD-sahipli, kapalı bir registry'dir** (`KnownScopes`). Scope'ları
  servisler kodda tanımlar; hiçbir ekran/DB yeni scope string'i **üretemez**. Rol→scope
  eşlemesi bu kapalı listeden **seçilir** (serbest metin yasak) — uyumsuzluk imkansız.
- **Rol ve rol→scope map DB'de yaşar; admin ekrandan yönetir.** Rol CRUD, rol→scope
  işaretleme ve rol→kullanıcı ataması yetkili admin tarafından yapılır. Register olan
  otomatik `customer` rolü alır (sunucu atar, seçilemez) ve **doğrudan login olabilir**
  (aktivasyon-mail zorunlu değil). Seed: `admin` + `customer` rolleri, rol→scope map ve
  login olabilen **bootstrap admin** kullanıcı (secret config'ten, kodda değil).
- **Back-office dahil her yüzey scope ile korunur.** Yönetim uçları (kullanıcı/rol
  yönetimi vb.) özel bir scope ister (ör. `identity.roles.manage`); bu scope admin
  rolünün demetindedir. Downstream servislerde rol-policy kullanılmaz.
- **İstisna — IdP'nin kendi admin UI'ı:** Bu scope kuralı **downstream API yüzeyleri**
  içindir. `Identity.Server`'ın KENDİ server-rendered yönetim sayfaları (rol/scope/kullanıcı
  yönetimi) rol otoritesinin iç yüzeyidir ve interaktif **cookie** ile auth olur — cookie'de
  scope claim'i bulunmaz, dolayısıyla cookie kullanıcısının `admin` rolüyle korunur. Bu, "rol
  ile yetki" değil, rol otoritesinin kendini koruması olduğundan İlke'yi ihlal etmez;
  `identity.roles.manage` scope'u programatik/API erişim için eşdeğer guard olarak tanımlı kalır.
- **Makine kimlikleri RBAC dışıdır.** Agent/saga gibi insan-olmayan çağıranlar
  `client_credentials` + **statik** scope ile kimliklenir (ClientId/Secret); rol, mail,
  kullanıcı kaydı taşımaz (ör. `ingestion-agent`, `order-saga`).
- Scope zorlaması endpoint'lerde `.RequireAuthorization(...)` ile, Wolverine mesaj
  handler'larında ise `[RequiredScope]` + `ScopeAuthorizationMiddleware` ile uygulanır.
- Kimlik doğrulama şeması JWT bearer ile sınırlı değildir: dış entegrasyonlar için
  JWT-olmayan custom authentication şemaları (ör. opak UserKey) meşrudur — koşul,
  zorlamanın scope olmasıdır.
- Anonim gezinme meşrudur: kimlik istemeyen okuma yüzeyleri (vitrin vb.) login'siz
  erişilebilir kalır; login yalnız kullanıcıya bağlı işlemler için istenir.
- `Identity.Server` HTTPS üzerinden çalışmak zorundadır; tüm servislerin `Authority`
  değeri issuer ile eşleşir.

### VI. Domain-TDD (saf domain mantığı test-first)

Saf domain mantığı **test-first (red-green-refactor)** yazılır. Kapsam: aggregate
davranış metotları, saga `On*` karar metotları, value object'ler ve mock'suz test
edilebilir diğer domain birimleri.

- Önce başarısız test (xUnit + Shouldly), sonra geçirecek en küçük kod, sonra refactor.
- `tasks.md`'de bu birimlerin test task'ları ilgili implementasyon task'ından ÖNCE gelir.
- Kapsam dışı: handler, endpoint, UI, altyapı/wiring — mevcut düzen sürer (test-sonra
  veya canlı doğrulama). Bu ilke için entegrasyon/host harness'ı KURULMAZ.
- Gerekçe: domain katmanı saf ve bağımlılıksızdır; TDD burada ucuz ve doğaldır,
  harness gerektiren katmanlarda pahalıdır. İlke II'nin "iş kuralı aggregate'te"
  kuralı bu ilkeyle test edilebilirliğini kanıtlar.

## Teknoloji ve Mimari Kısıtları

- **.NET 10**, C#, her yerde `Nullable` + `ImplicitUsings` açık.
- **Marten**, Postgres'i EF Core ile değil bir **document/event store** olarak kullanır.
- **Wolverine** hem süreç-içi command/query bus'ı hem RabbitMQ integration mesajlaşmasıdır;
  handler'lar assembly taramasıyla keşfedilir. Exchange/queue adları `RabbitMqConstants`
  içinde merkezidir.
- Sistem **her zaman Aspire AppHost üzerinden** çalıştırılır; tek servis bağımsız
  çalıştırılmaz (bağımlılıklarını service discovery ile bulur).
- **Central Package Management** açık: paket sürümleri `Directory.Packages.props`'a
  eklenir, tek tek `.csproj`'lara değil.
- **DI kaydı Scrutor ile otomatiktir:** `ITransientDependency` / `IScopedDependency` /
  `ISingletonDependency` marker'larından biri implemente edilir; `Program.cs`'te elle
  kayıt yapılmaz.
- Using'ler her projenin tek `GlobalUsings.cs`'ine eklenir.
- Agent / agent framework tipleri **Singleton**'dır; kullanıcıya özel davranış
  agent'ı scope'lamakla değil, kullanıcının token'ını çağrı anında enjekte ederek
  sağlanır.

## Geliştirme Akışı (Spec-Driven Development)

Önemsiz olmayan her feature spec-kit akışını izler:

1. `/speckit-constitution` — ilkeleri kur/güncelle (bu dosya)
2. `/speckit-specify` — feature spec'i (NE ve NEDEN)
3. `/speckit-clarify` (opsiyonel) — belirsizlikleri gider
4. `/speckit-plan` — implementation planı (NASIL)
5. `/speckit-tasks` — sıralı görevler
6. `/speckit-implement` — uygulama

### Artefakt Ölçekleme (feature büyüklüğüne göre)

Akış her feature için aynı **törenle** dayatılmaz; üretilen artefakt seti işin
büyüklüğüne göre ölçeklenir. Amaç, "boş-doğru" (mevcut durumu tekrar eden, yeni bilgi
katmayan) dosya üretmemektir. Üç kademe:

- **Trivial** — davranış değiştirmeyen ya da yeni iş kuralı getirmeyen değişiklik
  (bug fix, refactor, metin/konfig). Spec-kit gerekmez; doğrudan implement + commit.
- **Küçük** — *tümü* sağlanıyorsa: tek aggregate içinde kalır, yeni tablo/şema yok,
  yeni endpoint kontratı yok, servisler-arası (integration event) etki yok, kayda
  değer belirsizlik yok. Yalnızca **`spec.md` + `tasks.md`** üretilir;
  `plan/research/data-model/contracts/quickstart` **üretilmez**.
- **Tam** — yukarıdaki maddelerden biri bile bozuluyorsa (yeni aggregate/tablo,
  servisler-arası event, yeni endpoint kontratı, veya belirsizlik) tam akış işletilir.

Kademe seçimi spec'in başında bir satırla gerekçelendirilir. Şüphedeyse bir üst
kademe seçilir. `001-product-sale-readiness` retrospektif olarak "Küçük"tü.

Kalite kapıları:

- Testler xUnit + Shouldly ile yazılır; domain birim testleri saftır (host/entegrasyon
  harness'ı yoktur). Saf domain mantığı İlke VI gereği **test-first** yazılır.
- API sürümleme URL-segment tabanlıdır (`v1`); dokümanlar Scalar ile sunulur.
- Fiziksel klasörler solution klasörleriyle birebir örtüşür.

## Governance

- Bu anayasa diğer tüm pratiklerin üstündedir. Bir spec/plan/PR anayasayla
  çelişiyorsa, ya anayasaya uydurulur ya da bu dosyada gerekçelendirilmiş bir
  değişiklikle (amendment) güncellenir.
- Ayrıntılı, gündelik geliştirme rehberi `CLAUDE.md`'dir; anayasa "ne pazarlık
  edilemez", CLAUDE.md "nasıl uygulanır" sorusunu yanıtlar. İkisi çelişirse anayasa
  kazanır ve CLAUDE.md hizalanır.
- Değişiklikler (amendment) commit mesajında ve versiyon artışıyla belgelenir:
  ilke ekleme/kaldırma MAJOR, yeni ilke/bölüm ekleme MINOR, açıklama/düzeltme PATCH.

**Version**: 1.7.1 | **Ratified**: 2026-07-12 | **Last Amended**: 2026-08-06

<!-- v1.7.1 (2026-08-06, PATCH): İlke V'e açıklayıcı istisna eklendi — "back-office scope-gated"
     kuralı downstream API yüzeyleri içindir; Identity.Server'ın KENDİ server-rendered admin UI'ı
     cookie ile auth olur (cookie'de scope yok) ve cookie kullanıcısının admin rolüyle korunur.
     Bu rol-otoritesinin kendini koruması olduğundan İlke'yi ihlal etmez; identity.roles.manage
     programatik guard olarak kalır. Gerekçe: 030-rbac /speckit-analyze K1 gerginliğini kapatır. -->

<!-- v1.7.0 (2026-08-06, MINOR): İlke V keskinleştirildi — rol mekanizması netleşti. Rol token'a
     "claim biner" değil, token verme anında rol→scope map'inden SCOPE DEMETİNE açılır; downstream
     servisler rolü GÖRMEZ (rol claim ile yetki yok, [Authorize(Roles=...)] yok). Scope kümesi
     KOD-sahipli kapalı registry (KnownScopes); rol + rol→scope map DB'de, admin ekrandan yönetir,
     serbest scope string yazamaz (listeden seçer). Back-office dahil her yüzey scope ile korunur.
     Makine kimlikleri client_credentials + statik scope, RBAC dışı. Register aktivasyon-mail
     zorunlu değil (customer direkt login). Seed: admin+customer + rol→scope + bootstrap admin.
     Gerekçe: 2026-08-06 RBAC tasarım oturumu — granülarite endişesi (rol=scope demeti), ekrandan
     rol yönetimi + uyumsuzluk riski (kapalı scope registry), makine=client_credentials ayrımı. -->

<!-- v1.6.0 (2026-08-06, MINOR): İlke V yeniden yazıldı — IdP teknoloji-nötr (hedef OpenIddict +
     ASP.NET Identity; Duende lisans gerekçesiyle terk ediliyor) ve rol modeli geri geldi:
     register otomatik `customer` rolü (sunucu atar), yönetim yüzeyleri rol-policy (sabit adlı),
     servis akışlarında zorlama scope kalır, seed admin+customer + bootstrap admin, endpoint-bazlı
     DB yetki yasak, rol×permission katmanına açık. Gerekçe: 2026-08-06 tasarım oturumu —
     OpenIddict migrasyonu + kullanıcı/rol yönetim ekranları + e-posta aktivasyonlu register. -->

<!-- v1.5.0 (2026-08-05, MINOR): İlke VI (Domain-TDD) eklendi — saf domain mantığı test-first;
     tasks.md'de domain test task'ları implementasyondan önce. Handler/endpoint/UI kapsam dışı.
     Gerekçe: mülakat/TDD tartışması sonrası kullanıcı kararı; domain katmanı zaten mock'suz
     test edildiğinden TDD maliyeti düşük, entegrasyon harness'ı gerektirmiyor. -->

<!-- v1.4.0 (2026-08-05, MINOR): İlke I senkron RPC sanksiyonu orkestre edilmiş saga adım/telafi
     komutlarını kapsayacak şekilde genişletildi. Gerekçe: 028-checkout-saga — kullanıcı kararıyla
     tam orchestration; telafi (RevertCommit) ve pivot-sonrası temizlik (ClearBasket) hedefli komut
     ister, fanout event bunu modelleyemez. DB izolasyonu ve Shared/Protos kontrat kuralı aynen. -->

<!-- v1.3.1 (2026-07-28, PATCH): İlke I örneği güncellendi — Discount BC'si 018 ile
     kaldırıldığından örnek "Ürün" (Catalog aggregate / Basket entity / Storefront read-model
     satırı) olarak değiştirildi; ilke içeriği değişmedi. -->

<!-- v1.3.0 (2026-07-27, MINOR): İlke II gevşetildi — "her serviste tek aggregate root"
     yerine "BC gerektiği kadar zengin aggregate içerebilir; anemik aggregate yasak;
     aggregate'ler arası referans Id ile". Gerekçe: 016-category-brand — Catalog BC'ye
     Product yanına kimlikli Category ve Brand aggregate'leri (get-or-create, immutable ad)
     ekleniyor; VO modeli teklik invariant'ını taşıyamadığı için reddedildi. -->

<!-- v1.2.0 (2026-07-24, MINOR): İlke I'e tipli senkron RPC (gRPC/HTTP) sanksiyonlu
     servisler-arası kanal olarak eklendi (DB izolasyonu korunur). Gerekçe: stok
     rezervasyonu (spec 012) anlık evet/hayır kararı gerektirir; async event/MCP yetmez. -->