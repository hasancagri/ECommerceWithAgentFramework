# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Komutlar

Tüm komutlar repo kökünden çalıştırılır. Çözüm dosyası: `ECommerceWithAgentFramework.slnx`.

```bash
# Tüm çözümü derle
dotnet build

# Tüm dağıtık sistemi çalıştır (Aspire; servisleri, Postgres ve RabbitMQ'yu ayağa kaldırır)
dotnet run --project src/aspire/AppHost/AppHost.csproj

# Tüm testleri çalıştır
dotnet test

# Tek bir test projesini çalıştır
dotnet test tests/Basket.Api.Tests/Basket.Api.Tests.csproj

# İsimle tek bir testi çalıştır
dotnet test --filter "FullyQualifiedName~BasketTests.AddItem_AddsItemToBasket"
```

- **Sistemi her zaman Aspire AppHost üzerinden başlat**, tek tek servisleri değil — servisler birbirini, veritabanlarını ve RabbitMQ'yu Aspire service discovery ve connection-string enjeksiyonu ile bulur. Tek bir API'yi bağımsız çalıştırmak bağımlılıklarını bulamayacağı için başarısız olur.
- Central Package Management açık (`Directory.Packages.props`). **Paket sürümlerini oraya ekle/güncelle**, tek tek `.csproj` dosyalarına değil (bunlar `PackageReference`'ı sürümsüz listeler).

## Spec-Driven Development (spec-kit)

Önemsiz olmayan her feature **spec-kit** (GitHub Spec-Driven Development) akışıyla yürütülür. Akış Claude Code skill'leri üzerinden çalışır:

```
/speckit-constitution  # projenin pazarlık edilemez ilkeleri (.specify/memory/constitution.md)
/speckit-specify       # feature spec'i — NE ve NEDEN
/speckit-clarify       # (opsiyonel) belirsizlikleri gider
/speckit-plan          # implementation planı — NASIL
/speckit-tasks         # sıralı, uygulanabilir görevler
/speckit-implement     # uygulama
```

- **Anayasa (constitution) her şeyin üstündedir.** Projenin sert kuralları
  `.specify/memory/constitution.md` içinde yaşar (Bounded Context izolasyonu, zengin
  aggregate/invariant'lar, Vertical Slice + CQRS, Result pattern, scope-tabanlı yetki).
  Bir spec/plan/kod anayasayla çelişemez; çelişirse ya koda uydurulur ya da anayasa
  gerekçeli bir amendment ile güncellenir. Bu dosya (CLAUDE.md) **nasıl uygulanır**
  rehberidir; anayasa **ne pazarlık edilemez** sorusunu yanıtlar — çakışırsa anayasa kazanır.
- Kurulum yapısı `.specify/` (şablonlar, scriptler, workflow) altındadır; spec-kit
  komutları `.claude/skills/speckit-*` skill'leri olarak gelir. `.claude/settings.local.json`
  gitignore'dadır, `.claude/skills/` ise takip edilir.
- Doğrudan koda atlamadan önce en azından spec (ve gerekiyorsa plan) üretilir.
- **Artefakt seti feature büyüklüğüne göre ölçeklenir** (anayasadaki "Artefakt
  Ölçekleme" kuralı): _trivial_ değişiklik spec-kit'siz; _küçük_ feature (tek
  aggregate, yeni tablo/endpoint-kontratı/integration-event yok, belirsizlik yok)
  yalnızca `spec.md` + `tasks.md` üretir — `plan/research/data-model/contracts/quickstart`
  üretme; _tam_ feature (yeni aggregate/tablo, servisler-arası event, yeni kontrat
  veya belirsizlik) tam akıştan geçer. Şüphedeyse bir üst kademeyi seç.
- **Domain-TDD (anayasa İlke VI):** saf domain mantığı (aggregate davranış metotları,
  saga `On*` kararları, value object'ler) test-first yazılır; tasks.md'de test task'ı
  implementasyondan önce gelir. Handler/endpoint/UI/altyapı bu kuralın dışındadır;
  onlarda mevcut düzen (test-sonra veya canlı doğrulama) sürer, mimari kurallar
  (İlke III) aynen geçerlidir.

## Teknoloji Yığını

- **.NET 10**, C#, her yerde `Nullable` + `ImplicitUsings` açık.
- **.NET Aspire** — `src/aspire/AppHost` sistemi kurar: Postgres (pgAdmin ve kalıcı volume ile), RabbitMQ (management plugin) ve tüm servis/gateway/web/agent projeleri birer resource olarak.
- **Marten** (`9.5.0`) — kalıcılık (persistence). Postgres, EF Core ile değil, bir **document store / event store** olarak kullanılır. Serileştirme Newtonsoft iledir; non-public setter'lar + non-public default constructor'lar açıktır (böylece aggregate'ler private setter'larını korur).
- **Wolverine** (`6.4.1`) — iki iş yapar: (1) süreç-içi command/query bus'ı (`IMessageBus.InvokeAsync`) ve (2) RabbitMQ üzerinden integration mesajlaşması. Handler'lar assembly taramasıyla keşfedilir (`opts.Discovery.IncludeAssembly`).
- **OpenIddict** (`Identity.Server`, 029) — OIDC/OAuth sunucusu + **ASP.NET Identity** (kullanıcı store). Servisler JWT bearer ile kimlik doğrular, **scope** bazında yetkilendirir (aşağıya bak). `Duende.IdentityModel` yalnız istemci yardımcısı olarak KALIR (ücretsiz).
- **YARP** gateway (`src/services/gateway`) — Aspire service-discovery destination resolver ile.
- **MCP** (Model Context Protocol) — her API `/mcp` altında bir MCP sunucusu barındırır; `ChatAgent` ise bir MCP istemcisidir.
- **ChatAgent** — AI agent uygulaması; **Microsoft Agent Framework** (`Microsoft.Agents.AI.*`) + `Microsoft.Extensions.AI` (OpenAI) üzerine kurulu. Resource adı: `chat-agent`.
- **Scrutor** — DI otomatik kaydı (bkz. Konvansiyonlar).
- **Testler** — xUnit + Shouldly; saf domain birim testleri (host/entegrasyon harness'ı yok).

## Mimari

Mikroservisler `src/services/{basket,catalog,file,order,payment,stock,storefront,supplier}`
altında, ayrıca `gateway`. Destekleyici projeler: `src/others` (`Common`, `Shared`, `Identity.Server`),
`src/aspire` (`AppHost`, `ServiceDefaults`), `src/agents` (`ChatAgent`, `IngestionAgent`) ve
`src/ui` (`WebApp`). Fiziksel klasörler solution klasörleriyle birebir örtüşür.

**Her servis kendi Postgres veritabanına sahiptir** (`catalogDb`, `basketDb`, …; `AppHost.cs`'te bağlanır) ve kendi Marten şemasına (`SchemaConstants`). Servisler asla veritabanı paylaşmaz.

### Tedarikçi ingestion akışı (007)

- `Supplier.Api` dış dünya maketidir (DB'siz feed ucu); `Supplier.Gateway` sınır bileşenidir:
  feed'i çeker, kanonikleştirir, SON yayınlanan snapshot'la kıyaslar (`supplierGatewayDb`).
- Yalnız yeni/değişen kayıt `SupplierProductSnapshotReceived` event'iyle yayınlanır; sıra
  önce publish sonra save'dir (çökmede kayıp yerine tekrar; yazımlar idempotent).
- `IngestionAgent` DB'siz, state'siz tüketicidir: mesaj başına MAF workflow
  (BrandWrite → CategoryWrite → CatalogWrite → StockWrite, 016/018) koşar; her adım
  kendi servisine scope'lu bir LLM agent'ıyla (ChatClientAgent) MCP tool'larını çağırır (015).
  Kimlikler (BrandId/CategoryId/ProductId) tipli sonuçlarla adımlar arasında KOD ile taşınır;
  kategori zorunludur (boş kategori CategoryWrite'ta kesilir). Short-circuit conditional
  edge'lerdedir; her yol terminal collector'dan geçer. Model config'i (`OpenAI:ApiKey`+`Model`)
  zorunludur, açılışta fail-fast.
- Hata yolu: başarısız yazım `IngestionWriteException`'a çevrilir → kademeli sınırlı retry,
  tükenince mesaj içeriğiyle DLQ (`ingestion.supplier-product-snapshot.dlq`). Run API'si yok;
  görünürlük kuyruk derinliği + DLQ + loglardır.

### DDD ve Bounded Context

**Her mikroservis bir Bounded Context'tir.** Sınır fiziksel ve serttir: her context'in kendi veritabanı, kendi şeması ve kendi domain modeli vardır; ortak (paylaşılan) bir domain modeli **yoktur**.

- **Aynı kavram, farklı context'te farklı modeldir.** Örnek: "Ürün" hem `Catalog` hem `Basket` hem `Storefront` context'inde geçer ama aynı şey değildir. Catalog'da `Product` zengin bir **aggregate**'tir; Basket'te ürün, sepete alınmış ad+fiyat+adet taşıyan sade bir `BasketItem` **entity**'sidir; Storefront'ta ise ProductId-anahtarlı bir **read-model satırı**dır (`StorefrontView`). Bir context'in modelini diğerine sızdırma.
- **Context'ler arası iletişim sadece integration event'leri ve MCP ile olur** (bkz. aşağıdaki ilgili bölümler). Bir servisin başka bir servisin aggregate'ine, DbContext'ine veya tablosuna doğrudan erişmesi yasaktır. Paylaşılabilen tek şey, `Shared.IntegrationEvents`'teki event kontratları gibi bilinçli olarak paylaşılan sözleşmelerdir.

**Domain yapı taşları** (`Common.Domains` içindeki ortak temeller):

- **Aggregate Root** — `AggregateRoot` (→ `BaseUserTrackModel` → `BaseModel`) sınıfından türer; `Id`, denetim alanları (`CreatedTime`/`UpdatedTime`, soft-delete için `IsDeleted`, kullanıcı izleri `CreatedUserId`...) hazır gelir. **Her servis tek BC'dir; bir BC gerektiği kadar zengin aggregate root içerebilir, hepsi `AggregateRoot`'tan türer** (ör. `Basket`, `Order`, `Payment`, `ProductStock`; Catalog: `Product`+`Category`+`Brand`). Anemik (davranışsız) aggregate yasaktır; aynı BC içindeki aggregate'ler birbirine Id ile referans verir. Aggregate root **tutarlılık sınırıdır** — dış dünya aggregate'i yalnızca kök üzerinden değiştirir.
- **Entity** — aggregate içinde kimliği (`Id`) olan, ama bağımsız yaşamayan nesne. İki türlüdür: kimlik + denetim alanlarına ihtiyaç duyan entity `BaseModel`'den türer (ör. `OrderItem`); ihtiyaç duymayan sade entity ise base sınıf almaz (ör. `BasketItem`). Her iki durumda da private setter + davranış metotları kullanılır ve entity aggregate'e aittir. **`BaseModel`, aggregate root olmayan ama `Id`/denetim alanları gereken sınıflar için temeldir; `AggregateRoot`'u yalnızca aggregate kökleri için kullan.**
- **Value Object** — kimliği olmayan, değeriyle tanımlanan nesne; `record` olarak, private ctor + statik `Create` fabrikasıyla yazılır (`Address`).
- **Enumeration** — tip-güvenli enum'lar için `Enumeration` temel sınıfı (int enum yerine).

**Invariant'lar (değişmezler) aggregate'in içinde korunur.** Koleksiyonlar private tutulur ve yalnızca okunur olarak expose edilir (`_items` → `IReadOnlyList<BasketItem> Items`); mutasyon yalnızca aggregate metotlarından geçer (`AddItem`, `SetItem`...). Kural ihlali handler'da değil, aggregate'te yakalanır — ör. `Order.AddOrderItem` boş ürün adında hata Result'ı döner. **Yeni bir kural eklerken önce aggregate metoduna bak; iş mantığını handler'a değil aggregate'e koy.**

### Vertical Slice + DDD

Bir servis içinde kod teknik katmana göre değil, domain feature'ına göre düzenlenir:

```
Domains/<Aggregate>/
  <Aggregate>.cs                  # zengin aggregate root (private setter, factory + davranış metotları)
  <Aggregate>EndpointExtension.cs # feature endpoint'lerini gruplar + map'ler
  <Aggregate>McpTools.cs          # bu aggregate için MCP tool sarmalayıcıları
  Features/
    Commands/<Name>.cs            # yazma (write) slice'ları
    Queries/<Name>.cs             # okuma (read) slice'ları
    Agent/<Name>.cs               # agent'a açık slice'lar (MCP üzerinden expose edilir)
```

**Bir feature = bir static class**; ihtiyaç duyduğu her şeyi içine gömer: `record` command/query, `Response`, `Handler` (`Handle` metodu olan düz bir sınıf) ve endpoint-extension `static class`'ı. Örnek şekil:

```csharp
public static class AddBasketItem
{
    public record AddBasketItemCommand(...);
    public class AddBasketItemResponse { ... }

    [Transactional]
    public class AddBasketItemCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddBasketItemResponse>> Handle(
            AddBasketItemCommand cmd, IDocumentSession session, CancellationToken ct) { ... }
    }
}
```

Bu desenden doğan temel kurallar:

- **CQRS:** yazma ve okuma ayrı slice'lardır. Durumu değiştiren işlemler `Features/Commands/` altında (`IDocumentSession` ile yazar, handler `[Transactional]`), yalnızca veri döndürenler `Features/Queries/` altında (yalnızca okur) yer alır. Yeni bir feature eklerken önce onu command mı query mi diye ayır ve doğru klasöre koy; ikisini tek slice'ta birleştirme.
- **Repository yok.** Handler'lar kalıcılık için doğrudan Marten'ın `IDocumentSession`'ını, başka bir slice'ı çağırmak için `IMessageBus`'ı alır. Yazma handler'ları `[Transactional]` ile işaretlenir.
- **Endpoint'ler Minimal API'dir**; `*EndpointExtension` metotları üzerinden map'lenir ve `Program.cs`'ten çağrılır. Kullanıcıyı `CurrentUser.Load(httpContext.User)` ile çözer, handler'ı `IMessageBus.InvokeAsync` ile çağırır ve `.RequireAuthorization(AuthorizationScopes.Xxx)` ile korur.
- Handler'lar `FeatureObjectResultModel<T>` / `FeatureResultModel` döner (`Common.Results` içinde); endpoint `IsSuccess`'i `Ok`/`BadRequest`'e çevirir.
- **API sürümleme** URL-segment tabanlıdır (`v1`), her serviste ayrı yapılandırılır; dokümanlar Scalar ile uygulama kökünde sunulur.

### Result Pattern

Beklenen hatalar (bulunamadı, doğrulama, iş kuralı ihlali) **exception ile değil, bir Result nesnesiyle** taşınır. Handler'lar, aggregate metotları ve endpoint'ler her zaman bir Result döner; exception yalnızca gerçekten beklenmeyen durumlar içindir (onları da `GlobalExceptionHandler` yakalar).

Tüm sonuç tipleri `Common.Results` altındadır ve `BaseResultModel`'den türer (`IsSuccess`, hata taşıyıcısı `Messages: List<MessageItem>`, `LocalizedMessages`). Statik fabrika metotlarıyla üretilirler — `new` ile kurma:

- **`FeatureResultModel`** — veri döndürmeyen işlemler. `Ok()`, `Error(MessageItem)`, `NotFound()`.
- **`FeatureObjectResultModel<T>`** — tek nesne (`where T : class, new()`). `Ok(data)` verilen `data` null ise otomatik `NotFound()` döner.
- **`FeatureListResultModel<T>`** — liste; boş liste otomatik `NotFound()` olur.
- **`FeaturePagedResultModel<T>`** — sayfalı liste (PagedList.Core meta verisiyle).
- **`ResultDomain` / `ResultDomain<T>`** — domain katmanı içi sonuç varyantı.

Hata bilgisi `MessageItem` ile taşınır: `Property`, `Table`, `Code`, `Params`. **`Code` serbest metin değil, bir kaynak (resource) sabitidir** (ör. `StockResourceConstants.STOCK_INSUFFICIENT`) — yeni bir hata mesajı eklerken önce ilgili resource sabitini tanımla, sonra `Error(new MessageItem { Code = ... })` ile döndür.

**Her servis kendi hata kodlarının hepsine sahiptir.** Kod (generic ya da domain) `<Service>/Constants/<Service>ResourceConstants.cs`'te yaşar; "bu generic mi domain mi, Common'a mı gitsin" diye bakılmaz. `CommonResourceConstants` yalnız framework-içidir (Common'ın kendi `FeatureOutputModel`/`GlobalExceptionHandler`'ı emit eder); servisler ona referans vermez. Generic kodun servisler arası tekrarı BC izolasyonunda kabul edilir.

Endpoint'ler sonucu HTTP'ye çevirir: `result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result)`. Aggregate metotları da uygun olduğunda Result döner (ör. `Basket.RemoveItem` → `FeatureResultModel.NotFound()`).

### MCP tool'ları

Her servis agent'ın çağırabileceği tool'ları `*McpTools.cs` içinde açar (`[McpServerToolType]` / `[McpServerTool]`). **MCP tool YALNIZ bir Agent slice'ını (`Features/Agent/<X>ForAgent`) çağırır** — LLM'e uygun isim + `[Description]` ekler, iş mantığı taşımaz. MCP sunucusu `app.MapMcp("/mcp")` ile mount edilir. `ChatAgent` bunlara MCP istemcisi olarak bağlanır (kullanıcı token'ı çağrı anında enjekte edilir).

- **Agent/MCP yüzeyi izole.** Agent'a açık işlem `Domains/<Aggregate>/Features/Agent/` altında; slice adı `<X>ForAgent` (ör. `SubmitRegistrationForAgent`, `GetMerchantForAgent`).
- **Agent slice `Features/Commands/` veya `Features/Queries/` class'larına ASLA gitmez — `IMessageBus` ile bile değil.** Kendi Query/Command + Response + Handler'ını taşır; okumayı/işlemi `IDocumentSession` ile doğrudan yapar (kod tekrarı bilinçli).

- **Metin (chat) akışında MCP DOLAYLI kullanılır — elle `CallToolAsync` YOK.** Agent, uygulama-içi
  MCP tool'larını boot'ta toplar (allowlist) ve bir işi metinle isteyen kullanıcı için **LLM prompt
  üzerinden** uygun tool'u seçip çağırır. Bir metin-akışı işini gerçekleştirmek için C# içinde elle
  `McpClient.CreateAsync` + `CallToolAsync` yazıp agent'ın LLM tool-seçimini atlamak **yasaktır**.
  İş, agent'ın topladığı tool + prompt eşlemesiyle çözülür; yeni yetenek = yeni MCP tool + prompt satırı,
  imperatif MCP-çağrı kodu değil.
- İmperatif `CallToolAsync` yalnızca **metin-dışı yapısal yollarda** (ör. bir HTTP endpoint'in doğrudan
  sürüşü) kabul edilir; chat/agent akışına sızmaz.

### Integration event'leri (servisler arası)

Event kontratları `Shared.IntegrationEvents` içinde yaşar. Yayınlama/tüketme, Wolverine-üzerinden-RabbitMQ ile **fanout exchange**'ler kullanılarak yapılır; exchange/queue adları `RabbitMqConstants` içinde merkezileştirilmiştir. Her servis ihtiyaç duyduğu exchange/queue'ları kendi `Program.cs`'indeki `UseWolverine(...)` bloğunda tanımlar ve gelen event'leri `EventHandlers.cs`'te işler. Handler keşfi assembly taramasıyla olur; yani bir event handler'ın sadece keşfedilebilir bir `Handle`/`Consume` metodu olması yeterlidir.

### Senkron RPC (gRPC) — sanksiyonlu servisler-arası kanal (012)

Anlık tutarlılık gereken az sayıda akış için **senkron gRPC** kullanılır (constitution
v1.2.0 İlke I amendment); DB izolasyonu korunur — çağıran, çağrılanın DB'sine değil
API'sine erişir. Şu an tek kullanım **stok rezervasyonu**: `Basket`/`Order` → `Stock`.

- Proto kontratı paylaşılan: `src/others/Shared/Protos/stock_reservation.proto`. Stock sunucu
  (`GrpcServices=Server`), Basket/Order istemci (`Client`).
- Stock `StockReservationGrpcService` MCP/REST gibi ince sarmalayıcıdır: iş mantığı yok,
  Wolverine command'ini (`ReserveStock`/`ReleaseStock`/`CommitStock`) `IMessageBus` ile çağırır.
- Yetki: gRPC ucu `stock.reserve` scope'u ister; istemci `BearerForwardingHandler` ile
  kullanıcı token'ını iletir. WebApp BFF + Identity.Server `stock.reserve`'ü tanımlar/talep eder.
- **Rezervasyon modeli (Model B):** sepete ekleme `SetReservedQuantity` (idempotent, sabit
  TTL) ile rezervasyon tutar; sipariş `Commit` ile `OnHand`'i kalıcı düşürür; TTL dolunca
  Hangfire sweep `PurgeExpired` + `ReservationExpired` event'iyle sepet satırını temizler.
- **014 (Model C tersine döndü):** tedarikçi feed'i stoğun **tek otoritesidir**; IngestionAgent
  StockWrite geri geldi ve `OnHand`'i mutlak ezer. `OnHand` ayrıca sipariş Commit'iyle düşer.
- Fail-closed: Stock erişilemezse sepete **eklenmez** (oversell yasak).

### Checkout Saga — orchestration (028)

- Sipariş akışı **Wolverine durable saga** ile orkestre edilir: `CheckoutSaga` (Order BC,
  state Marten'da, Id=OrderId). Sipariş Pending doğar, HTTP hemen döner.
- Adımlar: kalem kalem gRPC StockCommit → Confirm → gRPC ClearBasket (Basket sunucu oldu).
  İş hatasında telafi: `RevertCommit` + `Cancel(reason)`. Sepet temizliği pivot-sonrası
  retryable adımdır — başarısızlığı siparişi iptal ETMEZ.
- Watchdog: scheduled `CheckoutTimedOut` (config `Checkout:WatchdogSeconds`, 120 sn);
  bitmemiş süreç telafi+iptal edilir. Karar mantığı saf `On*` metotlarında (birim testli).
- Arka plan yetkisi: kullanıcı bearer'ı YOK — `SagaTokenHandler` client-credentials
  `order-saga` makine token'ı alır (stock.reserve + basket.write).
- Idempotency: `Commit`/`RevertCommit` proto'da `order_id` anahtarı taşır; `ProductStock`
  işlenmiş operasyonları tutar (at-least-once teslimata dayanır).
- `OrderCreatedEvent` SİLİNDİ — sepet temizliği event'le değil saga adımıyla yapılır.
- Pivot kuralı: Confirm öncesi adımlar telafi edilir (compensatable); sonrası yalnız ileri
  gider (retryable) — pivot sonrası hata siparişi asla iptal ettirmez.
- Saga sürecin sahibi BC'de host edilir; ayrı orchestration servisi AÇILMAZ (god-service).
  Yeni saga eklerken karar mantığını saf `On*` metotlarında tut (mock'suz birim test).
- Gerekçe/öğrenme katmanı Obsidian: `adr-checkout-saga-orchestration` (stil seçimi, pivot,
  idempotency anahtarı, makine token'ı, canlı S1-S4 kanıtları, mülakat kartları).

### Önbellekleme (AOP, declarative — cross-cutting)

Okuma sorguları **handler'a kod yazmadan** önbelleklenir. Aspect `Common.Utils.Caching`'te yaşar
ve `IMessageBus`'ı şeffaf saran bir decorator'dır (`CachingMessageBus`, Scrutor `Decorate`);
endpoint ve handler değişmez. Motor **HybridCache** (L1 in-memory + opsiyonel L2 Redis).

- Bir query record'una `[Cached("tag", ttlSeconds)]` ekle → sonucu önbeklenir. `ttlSeconds` = **L2**
  Expiration; L1 TTL global (≤5sn), `AddCachingAspect(...)`'te ayarlı.
- Bir command record'una `[InvalidatesCache("tag")]` ekle → başarılı + commit sonrası `RemoveByTagAsync`
  ile iki katman boşalır. Negatif sonuç (NotFound) önbeklenmez.
- Servis `Program.cs`'te `UseWolverine`'den **sonra** `AddCachingAspect("<prefix>")` çağırır; L2 için
  Redis conn-string varsa `AddRedisDistributedCache("redis")`. Altyapı tüm servislerde hazır; tüketici: Customer.
- Anahtar VE tag BC-prefix'lidir (`CacheKeyFactory` + decorator) — paylaşımlı Redis'te BC izolasyonu.
- Çok-instance L1 tutarlılığı: boşaltma Redis pub/sub backplane'e yayınlanır (`cache-inv:{prefix}` kanalı);
  her instance `CacheBackplaneSubscriber` ile dinler, yerelini düşürür. At-most-once — ≤5sn L1 TTL güvenlik ağıdır.
- Neden middleware değil decorator: Wolverine `Before/After` short-circuit'te değer döndüremiyor
  (kanıtlandı). Gerekçe Obsidian `adr-aop-caching-mechanism`; sınır kararı `adr-cache-vs-readmodel`.

Cache kuralları (ne cache'lenir, kim boşaltır):

- Redis yalnız BC'nin KENDİ verisi için; başka BC'nin verisi gerekiyorsa cache değil read model (event aboneliği).
- Cache'lenebilirlik ölçütü: herkese-aynı + tekrar okunan + bayatlığa toleranslı. Per-user düşük-tekrar veri (Basket,
  Order) ve tutarlılık-kritik veri (Stock) cache'lenmez.
- Invalidation'ın sahibi veriyi DEĞİŞTİREN koddur. Command-BC'de `[InvalidatesCache]` (decorator); projeksiyon-BC'de
  (Storefront) event handler satırı yazdıktan sonra `RemoveByTagAsync` — kaynak BC hedefin cache'inden habersizdir.
- `[Cached]` tag'i ile boşaltma tag'i birebir aynı olmalı; tag = aggregate/veri-kümesi adı, entity'ye ayrı tag yok.
- Yazma yolunu besleyen query CACHE'LENMEZ (ör. sepete-ekleye fiyat veren tekil vitrin sorgusu) — bayat değer
  kalıcı kayda sızar. Cache yalnız ekrana giden sonuçlara.
- Kardinalite ölçütü: aynı parametre kombinasyonu tekrar gelmiyorsa (serbest filtre/arama) `[Cached]` koyma.
- Saga/event-handler mutasyonları decorator'dan geçmez — o yollarda invalidation elle yapılır veya kısa TTL'e bırakılır.
- Elle boşaltma HER ZAMAN `CacheInvalidator.InvalidateAsync` ile — doğrudan `RemoveByTagAsync` backplane'e yayılmaz.

### Yetkilendirme (scope-tabanlı; rol = scope demeti, 030)

- Kimlik `Identity.Server` (OpenIddict + ASP.NET Identity, 029) tarafından verilir. Servisler `AddAuthenticationAndAuthorizationExtension(config, ...scopes)` çağırır ve `AuthorizationScopes.CatalogRead` / `BasketWrite` gibi scope'lar ister.
- **Access token `scope` claim'i çoklu-değerdir** (Duende paritesi): OpenIddict RFC 9068 tek-string yerine, `ScopeClaimArrayHandler` JWT'de diziye çevirir; `RequireClaim("scope", x)` + `ScopeAuthorizationMiddleware` bugünkü gibi çalışır. Servis kodu değişmez.
- **DİKKAT (`ScopeClaimArrayHandler`):** `context.TokenType` URN'dir (`TokenTypeIdentifiers.AccessToken`), kısa hint `TokenTypeHints.AccessToken` DEĞİL. Guard'ı hint'le kıyaslarsan handler no-op olur; scope tek string kalır → 403 → WebApp sepet redirect döngüsü.
- **Rol = token verme anındaki scope demeti (030).** Kullanıcı TEK rol taşır; `AuthorizeEndpoint` granted scope'ları `ScopeResolver` ile `requested ∩ rol demeti`ne süzer (`RoleScopeQuery` DB'den rol scope'larını okur). Downstream servisler rolü GÖRMEZ — rol yalnız id_token'a biner (UI), access token'a girmez.
- **KnownScopes** (`Rbac/KnownScopes.cs`) kod-sahipli kapalı registry; rol→scope yazımı `AssignableScopeValidator` ile bu listeye kısıtlı (serbest metin yasak). Rol + rol→scope map DB'de (`RoleScope` tablosu), admin `/Admin/*` Razor Pages'ten yönetir (cookie admin-rol guard, İlke V IdP istisnası). Giriş: WebApp header koşullu "Yönetim" linki.
- **Register** otomatik `customer` rolü atar (direkt login, aktivasyon yok). **Seed** (`SeedHostedService`) idempotent: admin+customer + rol→scope map + bootstrap admin (config'ten parola) + `ingestion-agent` client. Makine kimlikleri (agent/saga) client_credentials + statik scope, RBAC dışı.
- Scope zorlaması **Wolverine mesaj handler'larına da** uygulanır: `[RequiredScope]` taşıyan her mesaj tipi için bir `ScopeAuthorizationMiddleware` çalışır.
- `Identity.Server` **HTTPS** üzerinden çalışmak zorundadır (`SameSite=None; Secure` cookie'leri düz HTTP'de sonsuz döngüye girer ve tüm servislerin `Authority` değeri issuer ile eşleşmelidir).

## Konvansiyonlar

- **Özlü yazım — her madde, görev ve cümle en fazla 150 karakter.** Tüm repo
  dokümanları için geçerlidir (spec, tasks, CLAUDE.md, constitution...), ileriye
  dönük; mevcut belgeler bu yüzden yeniden biçimlendirilmez. Sığmıyorsa maddeyi böl
  veya ayrıntıyı ilgili yere taşı; tasks.md ne yapılacağını listeler, nasılını değil.
- **Using'ler:** her projenin tek bir `GlobalUsings.cs`'i vardır. Paylaşılan namespace'leri dosyalara tek tek `using` serpiştirmek yerine oraya ekle.
- **`Domains/` yalnız domain barındırır.** Servise özel teknik sabitler (resource/hata kodları vb.) `Domains/` altına değil, `<Service>/Constants/` klasörüne konur.
  Namespace `<Service>.Constants`, `GlobalUsings.cs`'e eklenir. Hata kodları için sahiplik kuralı Result Pattern bölümünde (her servis kendi kodlarına sahip).
- **DI kaydı Scrutor ile otomatiktir:** `Common.Dependencies` içindeki `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` marker arayüzlerinden birini implemente et; `AddAllDependencies()` onu otomatik kaydeder. Bunları `Program.cs`'te elle kaydetme.
- Agent / agent framework tipleri **Singleton**'dır — framework bunları başlangıçta yakalar; kullanıcıya özel davranış, agent'ı scope'lamakla değil, kullanıcının token'ını çağrı anında enjekte ederek sağlanır.
- **Config — Options pattern (strongly-typed).** Bir config bölümü magic-string `config["A:B"]`
  ile OKUNMAZ; `Options/` altında bir POCO tanımlanır ve `AddOptionsExt` uzantısında bağlanır:
  `AddOptions<T>().BindConfiguration(nameof(T)).ValidateDataAnnotations().ValidateOnStart()`.
  Tüketici `IOptions<T>` değil **düz POCO `T`**'yi ctor'dan enjekte eder (POCO'yu unwrap eden
  Singleton kaydı). Section adı tip adıyla eşleşir; zorunlu alan DataAnnotations, türetilmiş değer
  computed property. Referans: `WebApp/Extensions/OptionsExt.cs`, `IdentityServerSettings`/`GatewayOption`.

## Kod standartları

- **Sonuç sözleşmesi.** Handler'dan çağrılan aggregate davranış/fabrika metotları
  `ResultDomain` / `ResultDomain<T>` döner — **void mutator dahil** (`Basket.AddItem`,
  `ProductStock.Increase`, `Payment.SetStatus`). Başarıda `Ok()`/`Ok(data)`; invariant/guard
  ihlali `Error(messages)` ile sinyallenir (exception atmak yerine — Result pattern'in amacı).
  Çağıran deseni: `var r = agg.Method(...); if(!r.IsSuccess) return <handler-error>(r.Messages);`
  (`<T>` için `r.Data!`). Veri dönen metot `ResultDomain<T>` (ör.
  `ProductStock.PurgeExpired` → `ResultDomain<IReadOnlyList<StockReservation>>`).
  **Muaf:** saf getter/sorgu (`GetTotalPrice`, `AvailableAt`, `IsExpiredAt`) sarılmaz;
  outcome-enum dönen metot enum'u `Ok(outcome)` ile taşır.
- **Aggregate-klasör.** `Domains/<X>/` klasörünün hemen altı tek bir `: AggregateRoot`
  barındırır; iç içe aggregate yoktur. İstisna: domain-service, seeder ve read-model tipleri
  aynı BC içinde ayrı yerleşebilir (aggregate değildir).
- **ValueObjects.** Bir aggregate'e bağlı standalone value object `<Aggregate>/ValueObjects/`
  altına konur (ör. `AddressBooks/ValueObjects/Address`), aggregate kökünde durmaz.
- **Aggregate — private helper YOK.** Ortak mantık private metoda çıkarılıp çağrılmaz,
  **inline** yazılır (kod tekrarı bilinçli). **VO MUAF** (VO'da private helper serbest).
- **Aggregate metodu yalnız handler'dan çağrılır.** Başka aggregate metodundan (factory
  dahil) ÇAĞRILMAZ; domain-içi tek çağrılan metot gövdesi çağırana inline edilir. **VO MUAF**.
- **Aggregate public metoduna iki not.** (1) `/// <summary>` metodun ne işe yaradığını yazar;
  (2) `/// <remarks>Handler: <Ad></remarks>` onu çağıran Handler tipini gösterir (çoklu handler
  virgülle; saga/event-handler de sayılır). Dış slice rename etkilemez. **VO MUAF**.