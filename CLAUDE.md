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

## Teknoloji Yığını

- **.NET 10**, C#, her yerde `Nullable` + `ImplicitUsings` açık.
- **.NET Aspire** — `src/aspire/AppHost` sistemi kurar: Postgres (pgAdmin ve kalıcı volume ile), RabbitMQ (management plugin) ve tüm servis/gateway/web/agent projeleri birer resource olarak.
- **Marten** (`9.5.0`) — kalıcılık (persistence). Postgres, EF Core ile değil, bir **document store / event store** olarak kullanılır. Serileştirme Newtonsoft iledir; non-public setter'lar + non-public default constructor'lar açıktır (böylece aggregate'ler private setter'larını korur).
- **Wolverine** (`6.4.1`) — iki iş yapar: (1) süreç-içi command/query bus'ı (`IMessageBus.InvokeAsync`) ve (2) RabbitMQ üzerinden integration mesajlaşması. Handler'lar assembly taramasıyla keşfedilir (`opts.Discovery.IncludeAssembly`).
- **Duende IdentityServer** (`Identity.Server`) — OIDC/OAuth. Servisler JWT bearer ile kimlik doğrular ve **scope** bazında yetkilendirir (aşağıdaki Yetkilendirme'ye bak).
- **YARP** gateway (`src/services/gateway`) — Aspire service-discovery destination resolver ile.
- **MCP** (Model Context Protocol) — her API `/mcp` altında bir MCP sunucusu barındırır; `ChatAgent` ise bir MCP istemcisidir.
- **ChatAgent** — AI agent uygulaması; **Microsoft Agent Framework** (`Microsoft.Agents.AI.*`) + `Microsoft.Extensions.AI` (OpenAI) üzerine kurulu. Resource adı: `chat-agent`.
- **Scrutor** — DI otomatik kaydı (bkz. Konvansiyonlar).
- **Testler** — xUnit + Shouldly; saf domain birim testleri (host/entegrasyon harness'ı yok).

## Mimari

Mikroservisler `src/services/{basket,catalog,discount,file,order,payment,stock}` altında, ayrıca `gateway`. Destekleyici projeler: `src/others` (`Common`, `Shared`, `Identity.Server`), `src/aspire` (`AppHost`, `ServiceDefaults`), `src/agents` (`ChatAgent`) ve `src/ui` (`WebApp`). Fiziksel klasörler solution klasörleriyle birebir örtüşür.

**Her servis kendi Postgres veritabanına sahiptir** (`catalogDb`, `basketDb`, …; `AppHost.cs`'te bağlanır) ve kendi Marten şemasına (`SchemaConstants`). Servisler asla veritabanı paylaşmaz.

### DDD ve Bounded Context

**Her mikroservis bir Bounded Context'tir.** Sınır fiziksel ve serttir: her context'in kendi veritabanı, kendi şeması ve kendi domain modeli vardır; ortak (paylaşılan) bir domain modeli **yoktur**.

- **Aynı kavram, farklı context'te farklı modeldir.** Örnek: "İndirim" hem `Basket` hem `Discount` context'inde geçer ama aynı şey değildir. Basket içinde `Discount` sadece sepete uygulanmış bir kupon+oranı temsil eden bir **value object**'tir (`Domains/Baskets/ValueObjects/Discount.cs`); Discount servisinde ise kendi `DiscountCode` / `DiscountRate` value object'leriyle yönetilen tam bir **aggregate**'tir. Bir context'in modelini diğerine sızdırma.
- **Context'ler arası iletişim sadece integration event'leri ve MCP ile olur** (bkz. aşağıdaki ilgili bölümler). Bir servisin başka bir servisin aggregate'ine, DbContext'ine veya tablosuna doğrudan erişmesi yasaktır. Paylaşılabilen tek şey, `Shared.IntegrationEvents`'teki event kontratları gibi bilinçli olarak paylaşılan sözleşmelerdir.

**Domain yapı taşları** (`Common.Domains` içindeki ortak temeller):

- **Aggregate Root** — `AggregateRoot` (→ `BaseUserTrackModel` → `BaseModel`) sınıfından türer; `Id`, denetim alanları (`CreatedTime`/`UpdatedTime`, soft-delete için `IsDeleted`, kullanıcı izleri `CreatedUserId`...) hazır gelir. **Her serviste tek bir aggregate root vardır ve `AggregateRoot`'tan türer:** `Basket`, `Product`, `Order`, `Payment`, `Discount`, `ProductStock`. Aggregate root **tutarlılık sınırıdır** — dış dünya aggregate'i yalnızca kök üzerinden değiştirir.
- **Entity** — aggregate içinde kimliği (`Id`) olan, ama bağımsız yaşamayan nesne. İki türlüdür: kimlik + denetim alanlarına ihtiyaç duyan entity `BaseModel`'den türer (ör. `OrderItem`); ihtiyaç duymayan sade entity ise base sınıf almaz (ör. `BasketItem`). Her iki durumda da private setter + davranış metotları kullanılır ve entity aggregate'e aittir. **`BaseModel`, aggregate root olmayan ama `Id`/denetim alanları gereken sınıflar için temeldir; `AggregateRoot`'u yalnızca aggregate kökleri için kullan.**
- **Value Object** — kimliği olmayan, değeriyle tanımlanan nesne; `record` olarak, private ctor + statik `Create` fabrikasıyla yazılır (`Discount`, `DiscountCode`, `DiscountRate`, `Address`).
- **Enumeration** — tip-güvenli enum'lar için `Enumeration` temel sınıfı (int enum yerine).

**Invariant'lar (değişmezler) aggregate'in içinde korunur.** Koleksiyonlar private tutulur ve yalnızca okunur olarak expose edilir (`_items` → `IReadOnlyList<BasketItem> Items`); mutasyon yalnızca aggregate metotlarından geçer (`AddItem`, `ApplyNewDiscount`...). Kural ihlali handler'da değil, aggregate'te yakalanır — ör. `ApplyNewDiscount` boş sepette `BASKET_IS_EMPTY` döner. **Yeni bir kural eklerken önce aggregate metoduna bak; iş mantığını handler'a değil aggregate'e koy.**

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

Hata bilgisi `MessageItem` ile taşınır: `Property`, `Table`, `Code`, `Params`. **`Code` serbest metin değil, bir kaynak (resource) sabitidir** (ör. `CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND`) — yeni bir hata mesajı eklerken önce ilgili resource sabitini tanımla, sonra `Error(new MessageItem { Code = ... })` ile döndür.

Endpoint'ler sonucu HTTP'ye çevirir: `result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result)`. Aggregate metotları da uygun olduğunda Result döner (ör. `Basket.RemoveItem` → `FeatureResultModel.NotFound()`).

### MCP tool'ları

Her servis agent'ın çağırabileceği tool'ları `*McpTools.cs` içinde açar (`[McpServerToolType]` / `[McpServerTool]`). **Bu tool'lar ince sarmalayıcılardır; aynı Wolverine command/query'sini `IMessageBus` üzerinden yeniden çağırır** — iş mantığı eklemezler, yalnızca LLM'e uygun bir isim + `[Description]` eklerler. MCP sunucusu `app.MapMcp("/mcp")` ile mount edilir. `ChatAgent` bunlara MCP istemcisi olarak bağlanır (kullanıcı token'ı çağrı anında enjekte edilir).

### Integration event'leri (servisler arası)

Event kontratları `Shared.IntegrationEvents` içinde yaşar. Yayınlama/tüketme, Wolverine-üzerinden-RabbitMQ ile **fanout exchange**'ler kullanılarak yapılır; exchange/queue adları `RabbitMqConstants` içinde merkezileştirilmiştir. Her servis ihtiyaç duyduğu exchange/queue'ları kendi `Program.cs`'indeki `UseWolverine(...)` bloğunda tanımlar ve gelen event'leri `EventHandlers.cs`'te işler. Handler keşfi assembly taramasıyla olur; yani bir event handler'ın sadece keşfedilebilir bir `Handle`/`Consume` metodu olması yeterlidir.

### Yetkilendirme (scope-tabanlı, rol yok)

- Kimlik `Identity.Server` (Duende) tarafından verilir. Servisler `AddAuthenticationAndAuthorizationExtension(config, ...scopes)` çağırır ve `AuthorizationScopes.CatalogRead` / `BasketWrite` gibi scope'lar ister.
- **Rol yoktur** — rol tabanlı yetkilendirme bilinçli olarak kaldırıldı; yalnızca scope kullan.
- Scope zorlaması **Wolverine mesaj handler'larına da** uygulanır: `[RequiredScope]` taşıyan her mesaj tipi için bir `ScopeAuthorizationMiddleware` çalışır.
- `Identity.Server` **HTTPS** üzerinden çalışmak zorundadır (`SameSite=None; Secure` cookie'leri düz HTTP'de sonsuz döngüye girer ve tüm servislerin `Authority` değeri issuer ile eşleşmelidir).

## Konvansiyonlar

- **Using'ler:** her projenin tek bir `GlobalUsings.cs`'i vardır. Paylaşılan namespace'leri dosyalara tek tek `using` serpiştirmek yerine oraya ekle.
- **DI kaydı Scrutor ile otomatiktir:** `Common.Dependencies` içindeki `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` marker arayüzlerinden birini implemente et; `AddAllDependencies()` onu otomatik kaydeder. Bunları `Program.cs`'te elle kaydetme.
- Agent / agent framework tipleri **Singleton**'dır — framework bunları başlangıçta yakalar; kullanıcıya özel davranış, agent'ı scope'lamakla değil, kullanıcının token'ını çağrı anında enjekte ederek sağlanır.