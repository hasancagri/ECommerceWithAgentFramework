# Phase 0 Research: Storefront Composite Read Model (Ürün Vitrin Görünümü)

> 2026-07-19 revizyonu: feature sipariş-merkezliden ürün (ProductId) merkezli vitrin
> görünümüne çevrildi.
>
> 2026-07-20 revizyonu (as-built, PR #10 + #11) — bu doküman as-built ile şu noktalarda
> güncellendi: madde 2 (3 ayrı doküman → tek `StorefrontView`), madde 5 (Bootstrap
> KALDIRILDI, saf push-only), madde 8 (event'lerden `OccurredAtUtc` çıktı; `StockChanged`
> `int Quantity` taşır), madde 9 (MCP tool KALDIRILDI). Eşzamanlılık: `.Sequential()` +
> Marten optimistic concurrency (aşağıda madde 2).

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

## 2. Projeksiyon deposu — TEK doküman, TEK anahtar: ProductId (as-built)

**Decision (2026-07-20 güncel)**: Tek flat `StorefrontView` dokümanı, `ProductId` ile
anahtarlı. Alanlar: `Name?`/`ImageUrl?`/`IsDeleted` (Catalog), `StockQuantity?` (Stock),
`DiscountRate?` (Discount), `IsAvailableForSale` (ayrı süreç). Her kaynak yalnız kendi
alanlarını `Apply*` ile yazar; satırı herhangi biri yaratabilir (kısmi satır geçerli).

**Eşzamanlılık (timestamp yok):**
- Kaynak-içi sıra: listener `.Sequential()` (FIFO) → geç gelen eski event oluşmaz →
  event'lerde timestamp guard'a gerek yok (bkz. madde 8).
- Kaynaklar-arası lost-update: Marten `UseOptimisticConcurrency(true)` + Wolverine
  `RetryTimes(5)` → aynı satıra eşzamanlı yazımda çakışan handler taze yükleyip uygular.

**Rationale**: İlk as-built (2026-07-19) 3 ayrı doküman kullanıyordu — satır-yaratma sıra
bağımlılığını yapısal olarak eritmek için. 2026-07-20'de kullanıcı tercihiyle tek satıra
dönüldü: denormalize composite satır, ileride listeleme/filtreleme (`WHERE
is_available_for_sale AND stock_quantity > 0 ...`) için doğal biçim. Sıra bağımlılığı
`.Sequential()` + optimistic concurrency ile çözüldü; kısmi satır Catalog alanlarını
nullable yaparak kabul edildi (`IsAvailableForSale` yayınlanmamış satırı kapılar).

FR-002/FR-003 ihlal edilmez: okuma tek `LoadAsync<StorefrontView>`, Storefront'un KENDİ
DB'sine; kaynak servislere ağ çağrısı yok.

**Alternatives considered**: 3 ayrı doküman (önceki as-built) — sıra bağımlılığını
tablo-ayrımıyla eritiyordu; tek satır + `.Sequential()`/optimistic concurrency aynı
güvenceyi verip listeleme'yi kolaylaştırdığı için tercih edildi (kullanıcıyla görüşüldü).

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

## 5. Bootstrap (ilk dolum) — KALDIRILDI (2026-07-20, PR #10)

**Decision (güncel)**: Bootstrap YOKTUR. `Storefront.Api` dışarı hiç senkron çağrı
yapmaz — saf **push-only**: yalnızca 3 event kuyruğunu dinler, gelen sorgulara cevap
verir. İlk tasarımdaki açılış-anı `IHostedService` (Catalog/Stock/Discount REST
uçlarını `HttpClient` ile çağıran toplu dolum) + `BootstrapIdentityServerSettings` +
ilgili named HttpClient'lar + AppHost'taki catalog/stock/discount referansları silindi.

**Rationale**: Kullanıcı duruşu — "kaynaklar yalnız bana push eder; dışarı bilgilerine
neden ihtiyacım olsun." Bootstrap saf push-only felsefesiyle çelişiyordu. **Bilinçli
bedel:** cold-start dolumu yok — Storefront DB'si boşken yalnız servis ayağa kalktıktan
SONRA oluşan/değişen ürünler görünür (greenfield varsayımı). Bu, FR-011'i geri alır.

**Alternatives considered**: Backfill'i kaynak-taraflı "hepsini yeniden yayınla"
event'iyle yapmak — ileride gerekirse; senkron pull'a dönülmez.

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
- Yeni event: `DiscountChangedEvent(ProductId, decimal? Rate)` — `Rate: null` = indirim
  kaldırıldı.

**Rationale**: Kullanıcıyla görüşüldü ve onaylandı (spec Assumptions: "geriye dönük
uyumluluk hedeflenmez"). Mevcut kullanıcı-bazlı model, ürüne özel indirim kavramıyla
uyuşmuyor — yarım yamalı bir köprü (ikisini bir arada tutmak) yerine net bir domain
değişikliği tercih edildi.

**Alternatives considered**: Kullanıcı-bazlı modeli koruyup yanına ürün-bazlı YENİ bir
aggregate eklemek — reddedildi; aynı isimde iki farklı "Discount" kavramı context içinde
kafa karışıklığı yaratır ve kullanıcının "kullanıcıya özel değil, ürüne özel indirim
olsun" talebiyle uyuşmaz (değiştirme, yanına ekleme değil).

## 8. Catalog/Stock yayın noktaları

**Decision** (2026-07-20 as-built: event'lerden `OccurredAtUtc` çıktı, Stock adet taşır):
- Catalog: `CreateProduct`, `UpdateProduct`, `DeleteProduct` handler'ları
  `ProductChangedEvent(ProductId, Name, ImageUrl, IsDeleted)` yayınlar (yeni event; mevcut
  `ProductCreatedEvent`e DOKUNULMAZ, o Stock'un başlangıç stok kaydı açması için kalır).
- Stock: `IncreaseStock`, `DecreaseStock` handler'ları VE mevcut `ProductCreatedHandler`'ın
  başlangıç stok kaydı açtığı an, `StockChangedEvent(ProductId, Quantity)` yayınlar (gerçek
  adet; in-stock Storefront'ta `Quantity > 0`'dan türetilir).

**Rationale**: Mevcut yazma noktaları zaten `[Transactional]` + `IDocumentSession`
kullanıyor; yeni `bus.PublishAsync(...)` çağrısı aynı transaction'a (outbox) dahil olur,
ek altyapı gerekmez.

## 9. MCP — KALDIRILDI (2026-07-20)

**Decision (güncel)**: Storefront için MCP tool YOKTUR. İlk as-built'te
`StorefrontMcpTools.cs` (`get_product_storefront_view`) `GetProductStorefrontView`
sorgusunu MCP'ye açıyordu; kullanıcı kaldırdı. `app.MapMcp("/mcp")` mount'u kalabilir ama
açılan tool yok.

**Rationale**: Kullanıcı tercihi — agent'ın şu an storefront view tool'una ihtiyacı yok.
Gerekirse ince `IMessageBus.InvokeAsync` sarmalayıcısı olarak geri eklenebilir.