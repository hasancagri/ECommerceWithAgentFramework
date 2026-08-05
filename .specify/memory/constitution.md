<!-- Sync Impact Report — v1.3.1 → v1.4.0 (2026-08-05, MINOR)
     Modified: İlke I senkron RPC sanksiyonu genişletildi — "anlık evet/hayır kararı"na ek
     olarak, sürecin sahibi BC'de koşan orkestre edilmiş saga'ların adım/telafi komutları
     (028 checkout saga: Order→Stock RevertCommit, Order→Basket ClearBasket) meşru kullanım.
     DB izolasyonu ve kontrat zorunluluğu değişmedi. Runtime docs: CLAUDE.md 028 ile hizalanacak. -->

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

### V. Scope-Tabanlı Yetkilendirme (Rol Yok)

Kimlik `Identity.Server` (Duende) tarafından verilir; servisler JWT bearer doğrular
ve **scope** bazında yetkilendirir (`AuthorizationScopes.*`).

- **Rol yoktur** — rol tabanlı yetkilendirme bilinçli olarak kaldırılmıştır; yalnızca
  scope kullanılır.
- Scope zorlaması endpoint'lerde `.RequireAuthorization(...)` ile, Wolverine mesaj
  handler'larında ise `[RequiredScope]` + `ScopeAuthorizationMiddleware` ile uygulanır.
- Kimlik doğrulama şeması JWT bearer ile sınırlı değildir: dış entegrasyonlar için
  JWT-olmayan custom authentication şemaları (ör. opak UserKey) meşrudur — koşul,
  yetkinin **yine scope-tabanlı** kalması ve rol getirilmemesidir. İlkenin özü
  mekanizma değil, "scope, rol değil"dir.
- `Identity.Server` HTTPS üzerinden çalışmak zorundadır; tüm servislerin `Authority`
  değeri issuer ile eşleşir.

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
  harness'ı yoktur). Yeni kural/aggregate davranışı test edilir.
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

**Version**: 1.4.0 | **Ratified**: 2026-07-12 | **Last Amended**: 2026-08-05

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