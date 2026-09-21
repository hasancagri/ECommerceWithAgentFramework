# Phase 0 Research: Kampanya İndirim Motoru (Discount.Api)

Tüm NEEDS CLARIFICATION brainstorm oturumunda (2026-09-21) çözüldü; bu belge kararları + gerekçeleri +
reddedilen alternatifleri kayda geçirir. Somut kod desenleri 3 paralel keşif agent'ından (Payment.Api
iskeleti, Storefront read-model, event+gRPC) çıkarıldı.

## Karar 1 — Kapsam: yalnız kampanya (kupon ertelendi)

- **Karar**: v1 = kategori/yazar/yayınevi/tek-kitap süzgeçli yüzde kampanya. Kupon (kod, redemption, per-müşteri limit,
  public/private keşif) G6.1'e.
- **Gerekçe**: Kampanya görünür değerin çoğu (kitapyurdu "%X indirimli, T'ye kadar"); herkese-aynı →
  anonim vitrin read-model'ine oturur. Kupon müşteriye-özel/gizli → ayrı yüzey + aggregate + defter =
  ayrı alt-domain. YAGNI.
- **Alternatif red**: "Tam paket baştan" — kupon makinesi feature'ı geciktirir, çekirdek değeri riske atar.

## Karar 2 — İndirim NEREDE: vitrine push + checkout canlı doğrula

- **Karar**: Kampanya yüzdesi event'le Storefront'a itilir (gösterim); son para checkout'ta Discount.Api
  gRPC ile canlı doğrulanır (fiyat otoritesi).
- **Gerekçe**: Vitrin herkese-aynı → push read-model deseni (067/069 zaten böyle). Para güvenliği vitrin
  snapshot'ına güvenemez → checkout canlı. BC-arası S2S = gRPC (İLKE I).
- **Alternatif red**: "Her şey checkout-zamanı" (vitrinde indirim görünmez, kitapyurdu değil); "Basket
  indirimi taşır" (discount mantığı Basket'e sızar, bayat kalır).

## Karar 3 — Discount.Api fiyat TUTMAZ (saf yüzde otoritesi)

- **Karar**: Discount.Api yalnız kampanya + `ProductDiscount` (kitap-başı) + `ProductCatalogRef` tutar;
  **aktif yüzdeyi** verir. Etkin fiyatı tüketici (Storefront gösterim, Order checkout) kendi liste fiyatından
  hesaplar.
- **Gerekçe**: BC izolasyonu — fiyat Catalog/Storefront/Order'ın; Discount.Api'ye kopyalamak sızıntı +
  senkron borcu. Yüzde-only + kitap-başı-tek-indirim olunca hesap liste-bağımsız. Liste fiyatı değişince
  vitrin otomatik doğru hesaplar — ekstra push yok.
- **Alternatif red**: Discount.Api etkin fiyatı hesaplayıp itsin → fiyatı çoğaltır, price-change'de
  yeniden-push tetikçisi gerekir.

## Karar 4 — Süre yönetimi: per-kampanya Wolverine scheduled message

- **Karar**: Oluşunca `startsAt`→`CampaignActivated`, `endsAt`→`CampaignEnded` scheduled message
  (`ScheduleAsync`). start fire → kampanyanın kitaplarına indirim uygula+it; end fire → o kampanyanın
  `ProductDiscount`'larını sil → `ProductDiscountChanged(pct:0)` it (temizle). Handler **guard'lı idempotent**
  (bayat mesaj no-op; iptal gerekmez). Restart'ta durable geç fire. **View-guard yedek** (`now` pencere
  dışıysa vitrin gizler).
- **Gerekçe**: Kesin (tam bitişte temizler). Scheduling **kampanya sayısıyla** ölçeklenir (1 kampanya =
  ≤2 mesaj, kapsamı kaç kitap olursa olsun — kitap-başı schedule DEĞİL) — onlarca kampanya = ~100 durable
  envelope, önemsiz. Projede kanıtlı (077 PaymentIntentExpiry, 060 price-alarm).
- **Alternatif red**: (a) Saf okuma-anı guard → veri bayat kalır, devretme hiç olmaz (kullanıcı reddetti).
  (b) Periyodik reconcile sweep → coarse, gecikme; scheduled message daha kesin. (c) Per-kampanya timer'ın
  "edit/cancel reschedule" derdi guard'lı-idempotent-handler ile çözüldü (iptal yok, fire güncel duruma bakar).

## Karar 5 — Kitap başına tek indirim + snapshot süzgeç (BestWins YOK)

- **Karar**: Kitap merkez. Admin süzgeçle (kategori/yazar/yayınevi/tek-kitap) indirim açar; süzgeç
  **uygulama anında** kitap setine çözülür (snapshot); her kitaba `ProductDiscount` **yoksa** işlenir,
  zaten indirimli kitap **atlanır** (kitap başına TEK indirim, PK teklik). Çakışma = ilk-gelen-kazanır.
- **Gerekçe**: Gerçek-hayat karşılığı = "manuel kampanya/promosyon listesi" + "zaten indirimli olanı koru"
  merchandising kilidi. Overlap/BestWins/scope-key motoru tümden düşer → çok daha basit, tek-yol kod.
  Kitabın kategori-derinliği tartışması da erir (süzgeç uygulama anında kitabı zaten çözer).
- **Alternatif red**: (a) En-iyi-kazanır/scope-key dinamik matching — overlap motoru + çok-boyut hesabı
  (kullanıcı "karışıyor" bulup vazgeçti). (b) Dinamik fiyat kuralı (gelecekteki kitapları otomatik kapsa)
  — kurumsal, ileri seviye → G6.1. Snapshot v1 için yeter (admin gerekirse yeniden uygular).

## Karar 6 — Sepette expiry: grace yok

- **Karar**: Ürün sepetteyken kampanya biterse checkout indirim uygulamaz; müşteri liste fiyatını öder.
- **Gerekçe**: FR-005 canlı doğrulama zaten "ödeme anı geçerli"yi bağlar; sepete-ekleme anını kilitlemek
  ek state + tutarsızlık. İndirim = ödeme anında aktif olan.

## Kod deseni bulguları (keşiften)

- **Yeni BC iskeleti** = `src/services/payment/Payment.Api` şablonu: Program.cs Marten `AddMarten`
  (`.DocumentAlias` + `.ApplyAllDatabaseChangesOnStartup`), Wolverine `UseWolverine`+RabbitMQ+`IncludeType`,
  `AddAllDependencies` (Scrutor), MCP `MapMcp`. AppHost: `postgres.AddDatabase("discountDb")` + `AddProject`
  + `WithReference` + `WaitFor`. Sürümler `Directory.Packages.props`.
- **Admin MCP allowlist** (070/074): `Shared/McpToolNames.cs` → yeni `DiscountAdminTools`; Program.cs
  `ConfigureSessionOptions`'ta `discountAdminToolNames` allowlist + `AddMcpAdminResourceMetadata(...,"discount",...)`.
- **Storefront push**: `StorefrontView.cs` alanlar + `ApplyDiscount` metodu; yeni `DiscountConsumers.cs`
  (`ProductDiscountChanged`→ApplyDiscount) + Program.cs `IncludeType` + queue binding; `StorefrontSellableSchema.cs`
  Columns'a `discount_pct/starts_at/ends_at` + `BuildViewDdl` CASE (view-guard `effective_price`);
  `QueryStorefront.cs` SchemaBlock + `check-agent-query-schema.sh` guard.
- **Checkout gRPC**: `Shared/Protos/discount_query.proto` (`GetProductDiscounts`); Discount.Api
  `DiscountQueryGrpcService`; Order.Api `AddGrpcClient<DiscountQueryClient>().AddHttpMessageHandler<SagaTokenHandler>()`,
  `SagaTokenHandler` scope'una `discount.read` ekle; `StartPayment` handler'da discount uygula → tutar.
- **Event kontratı**: `Shared/IntegrationEvents.cs` → `ProductDiscountChanged(ProductId, DiscountPct,
  StartsAt, EndsAt)`; `RabbitMqConstants` discount exchange (fanout, yayıncı Discount.Api deklare eder,
  binding'i Storefront kurar — 007 dersi). Discount.Api `ProductChangedEvent` TÜKETİR (ProductCatalogRef besleme).