# Phase 0 Research: Storefront Composite Read Model (Ürün Vitrin Görünümü)

> 2026-07-19 revizyonu: feature sipariş-merkezliden ürün (ProductId) merkezli vitrin
> görünümüne çevrildi. Bu doküman güncel (ürün-merkezli) tasarımı yansıtır.

## 1. Depolama motoru ve okuma-yazma yaklaşımı

**Decision**: Yeni `Storefront.Api` servisi — mevcut repo konvansiyonuyla aynı, Marten
document store (Postgres), `storefrontManagement` şeması, Wolverine ile
`.IntegrateWithWolverine()` (transactional outbox). Rich aggregate YOK; projeksiyonlar
düz Marten dokümanları (invariant taşımaz).

**Rationale**: Repo'daki her serviste aynı stack; yeni teknoloji eklemek gereksiz
karmaşıklık. Marten'ın `IDocumentSession.LoadAsync<T>(id)` / `Store(T)` Id-bazlı upsert'i
event-tetikli projeksiyon güncellemesi için doğrudan uygun.

**Alternatives considered**: Ayrı NoSQL/Redis tabanlı read-model deposu — reddedildi;
var olan Postgres+Marten altyapısını tekrar kullanmak operasyonel yükü sıfırlar.

## 2. Projeksiyon parçalama stratejisi — TEK anahtar: ProductId

**Decision**: 3 ayrı, tek-kaynaklı Marten dokümanı, **hepsi `ProductId` ile anahtarlı**:

- `CatalogInfo` (ProductId) — Catalog yazar: `Name`, `ImageUrl`, `IsDeleted`.
- `StockInfo` (ProductId) — Stock yazar: `IsInStock`.
- `DiscountInfo` (ProductId) — Discount yazar: `Rate` (nullable — indirim yoksa `null`).

Sipariş-merkezli önceki tasarımdan farklı olarak, artık üç kaynağın da **aynı doğal
anahtarı** (ProductId) paylaşması nedeniyle OrderId/PaymentId gibi ikincil bir join
anahtarına gerek YOK — okuma sorgusu tek bir `ProductId` ile üçünü de doğrudan çeker.

**Rationale**: Kaynaklar birbirinden bağımsız, sırasız ve bazen hiç gelmeyen event'ler
yayınlar (Edge Cases: "sırasız event", "kısmi satır"). Ayrık projeksiyon, her kaynağın
SADECE kendi tablosuna, diğerinin varlığından habersiz yazmasını sağlar. Tek flat
doküman (3 alanı birden taşıyan) seçilseydi, üç kaynaktan biri ürünün ilk defasında
event yayınladığında satırın "yaratılması" ile diğer ikisinin "güncellemesi" arasında
sıra bağımlılığı oluşurdu; ayrı tablo bunu tamamen ortadan kaldırır (kullanıcıyla
görüşülüp onaylandı).

FR-002/FR-003 ("okuma tek istekte döner", "okuma anında 3 kaynağa senkron çağrı
yapılmaz") ihlal edilmez: üç tablo arası "join" Storefront'un KENDİ veritabanına yapılan
lokal sorgudur, Catalog/Stock/Discount servislerine ağ çağrısı değildir.

**Alternatives considered**: Tek flat `ProductStorefrontView` dokümanı — kullanıcıyla
görüşülüp reddedildi; ayrık tablo aynı gözlemlenebilir sonucu üretir, iç depolamayı
basitleştirir.

## 3. Event içeriği: fat (self-contained)

**Decision**: Her "değişti" event'i, Storefront'un o alanı güncellemek için ihtiyaç
duyduğu TÜM veriyi taşır; Storefront event'i aldıktan sonra kaynağa geri dönüp ek veri
çekmez (pull-back yok).

**Rationale**: Repo'da emsal var — `ProductCreatedEvent` bugün `ProductStockInfo`
listesini doğrudan taşıyor. Fat event, Storefront'u kaynaklardan TAMAMEN bağımsız
(conformist/downstream) tutar. Thin+pull-back, FR-002/003'ün ortadan kaldırdığı senkron
bağımlılığı event-sonrası bir çağrıyla geri sokardı.

**Alternatives considered**: Thin event + MCP ile geri dönüp detay çekme — reddedildi.

## 4. Dayanıklı yayın (outbox)

**Decision**: Ayrı bir outbox mekanizması KURULMAZ — repo'daki her servis zaten
`AddMarten(opts => ...).IntegrateWithWolverine()` ile Wolverine+Marten transactional
outbox entegrasyonunu kullanıyor. Yeni publish noktaları (Catalog/Stock/Discount) bu
davranışı otomatik devralır.

**Rationale**: Doğrulanmış mevcut altyapı; sıfırdan outbox tasarımı gereksiz risk.

## 5. Bootstrap (ilk dolum) mekanizması

**Decision**: `Storefront.Api` içinde bir kerelik, açılışta çalışan bir
`IHostedService`. Catalog'un ürün-listeleme (`GET api/v1/products`), Stock'un
stok-listeleme (`GET api/v1/stocks/all`), Discount'un ürün-indirimi-listeleme
(`GET api/v1/discounts/all`) REST uçlarını düz `HttpClient` ile çağırarak toplu
doldurma yapar. Aynı upsert mantığı (idempotent `Create`) event handler'larıyla
paylaşılır. `m2m.client` (Identity.Server, mevcut) ile client_credentials token alınır.

**Rationale**: Geçmiş event'ler RabbitMQ'da kalıcı değil. İlk tasarım (revizyon
öncesi) MCP tool'larını çağırmayı öngörmüştü — implementasyon sırasında (kullanıcıyla
görüşülüp) düz HTTP'ye çevrildi: MCP-client'ı elle sürmek (JSON-RPC tool-call framing,
gevşek-tipli sonuç ayrıştırma) repo'da ilk kez bir backend servisi (agent değil) MCP
client'a çevirirdi; düz `GetFromJsonAsync<T>` çok daha basit/sağlam ve hedef uç zaten
servisin kendi genel REST kontratı (DB'sine değil) — izolasyon ilkesi ihlal edilmiyor.
Bootstrap bir kerelik arka plan işi olduğu için FR-003'ün yasakladığı "okuma anında
senkron çağrı" ile çakışmaz.

**Alternatives considered**: Kaynakların geçmiş event'lerini "replay" etmesi için özel
endpoint — reddedildi, mevcut List/GetAll REST uçları zaten yeterli. MCP tool'ları
üzerinden çağırmak (orijinal karar) — implementasyonda reddedildi, bkz. Rationale.

## 6. Erişim / yetkilendirme

**Decision**: Görünüm herkese açıktır (kimlik doğrulaması/kullanıcı girişi gerekmez)
ama endpoint yine bir scope ister: yeni `AuthorizationScopes.StorefrontRead`
(`storefront.read`). Bu scope, `WebApp/Authentication/TokenService.cs`'teki mevcut
anonim M2M `ReadScopes` listesine (`"catalog.read discount.read stock.read"`) eklenir
— repo'da zaten var olan "kullanıcı login değilken WebApp'in kendi client_credentials
kimliğiyle public okuma yapması" deseni (bkz. `catalog.read`/`stock.read`, `Products/
Detail.cshtml.cs`'in `[AllowAnonymous]` olması).

**Rationale**: Anayasa madde V scope-tabanlı yetkilendirmeyi zorunlu kılıyor (rol yok,
ama scope var) — "herkese açık" olması, sıfır yetkilendirme değil, var olan anonim-M2M
deseniyle aynı şekilde ele alınması anlamına gelir. Kullanıcı seviyesinde login/ownership
kontrolü YOKTUR (FR-007) — WebApp zaten kendi client kimliğiyle bu scope'u talep eder.

**Alternatives considered**: Endpoint'i tamamen `AllowAnonymous` + scope'suz bırakmak —
reddedildi, repo konvansiyonuyla (her endpoint bir scope ister) tutarsız olurdu.

## 7. Discount.Api'nin ürün-bazlı modele dönüşümü

**Decision**: `Discount` aggregate'i kullanıcı-bazlı (`UserId`, `DiscountCode`,
`DiscountRate`) modelden **ürün-bazlı** (`ProductId`, `DiscountRate`) modele
dönüştürülür:

- `Discount.Create(productId, rate)` — `DiscountCode` value object'i ve kupon-kodu
  kavramı KALDIRILIR (ürün-özel indirim bir "kod" ile redeem edilmez, doğrudan üründe
  görünür).
- Bir ProductId için en fazla 1 aktif `Discount` kaydı olabilir (FR-012/SC-006) —
  `SetProductDiscount` komutu var olanı günceller (upsert), `RemoveProductDiscount`
  komutu kaldırır.
- `EventHandlers.cs`'teki `OrderCreatedHandler` (sipariş sonrası otomatik kupon üretimi)
  ve `DiscountCodeGenerator` KALDIRILIR — bu davranış artık anlamsız (Discount artık
  OrderCreatedEvent'i dinlemez).
- `GetDiscountByCode` sorgusu/endpoint'i `GetDiscountByProductId` ile DEĞİŞTİRİLİR.
- Yeni event: `DiscountChangedEvent(ProductId, decimal? Rate, DateTime OccurredAtUtc)`
  — `Rate: null` = indirim kaldırıldı.

**Rationale**: Kullanıcıyla görüşüldü ve onaylandı (spec Assumptions: "geriye dönük
uyumluluk hedeflenmez"). Mevcut kullanıcı-bazlı model, ürüne özel indirim kavramıyla
uyuşmuyor — yarım yamalı bir köprü (ikisini bir arada tutmak) yerine net bir domain
değişikliği tercih edildi.

**Alternatives considered**: Kullanıcı-bazlı modeli koruyup yanına ürün-bazlı YENİ bir
aggregate eklemek — reddedildi; aynı isimde iki farklı "Discount" kavramı context içinde
kafa karışıklığı yaratır ve kullanıcının "kullanıcıya özel değil, ürüne özel indirim
olsun" talebiyle uyuşmaz (değiştirme, yanına ekleme değil).

## 8. Catalog/Stock yayın noktaları

**Decision**:
- Catalog: `CreateProduct`, `UpdateProduct`, `DeleteProduct` handler'ları
  `ProductChangedEvent(ProductId, Name, ImageUrl, IsDeleted, OccurredAtUtc)` yayınlar
  (yeni event; mevcut `ProductCreatedEvent`e DOKUNULMAZ, o Stock'un başlangıç stok kaydı
  açması için farklı bir amaçla kalır).
- Stock: `IncreaseStock`, `DecreaseStock` handler'ları VE mevcut
  `ProductCreatedHandler`'ın başlangıç stok kaydı açtığı an,
  `StockChangedEvent(ProductId, IsInStock, OccurredAtUtc)` yayınlar (`IsInStock` =
  `Quantity > 0`).

**Rationale**: Mevcut yazma noktaları zaten `[Transactional]` + `IDocumentSession`
kullanıyor; yeni `bus.PublishAsync(...)` çağrısı aynı transaction'a (outbox) dahil olur,
ek altyapı gerekmez.

## 9. MCP

**Decision**: `Storefront.Api`, `StorefrontMcpTools.cs` ile `GetProductStorefrontView`
sorgusunu ince bir sarmalayıcı olarak MCP'ye açar (`app.MapMcp("/mcp")`).

**Rationale**: Repo konvansiyonu (CLAUDE.md: "Her servis agent'ın çağırabileceği
tool'ları açar").