# Research — Stok Rezervasyonu (Model B)

Phase 0. Tüm NEEDS CLARIFICATION ve teknik seçim noktaları burada karara bağlanır.

## R1. Servisler-arası senkron kanal: gRPC + Aspire service discovery

- **Decision:** Basket→Stock ve Order→Stock çağrıları **gRPC** ile yapılır. Stock bir
  gRPC sunucusu (`StockReservationGrpcService`) barındırır; Basket/Order gRPC istemcisidir.
  Adresleme Aspire service discovery ile (`https://stock-api`), `AddServiceDiscovery` +
  `AddStandardResilienceHandler` (ServiceDefaults'ta zaten var) üzerinden.
- **Rationale:** Rezervasyon anlık evet/hayır ister; tipli sözleşme + HTTP/2. Paket
  (`Grpc.AspNetCore 2.67.0`) zaten `Directory.Packages.props`'ta. Kullanıcı MCP'yi
  servisler-arası reddetti, gRPC'yi seçti.
- **Alternatives:** HttpClient (typed) — çalışır ama sözleşme zayıf; kullanıcı gRPC dedi.
  Async event — anlık karar veremez. MCP — reddedildi.

## R2. Anayasa amendment (İlke I)

- **Decision:** `.specify/memory/constitution.md` İlke I'e senkron **gRPC/HTTP RPC**'yi
  sanksiyonlu bir servisler-arası kanal olarak ekleyen bir amendment yapılır (MINOR →
  **v1.2.0**). Koşul: DB izolasyonu korunur (bir servis diğerinin DB/aggregate'ine
  doğrudan erişmez); RPC yalnız tipli kontrat üzerinden ve request/response gereken
  senkron kararlar için kullanılır. `/speckit-constitution` ile ratifiye edilir.
- **Rationale:** Governance: anayasayla çelişen plan ya uyar ya amendment'la güncellenir.
  Yeni kanal eklemek = MINOR (ilke genişletme).
- **Alternatives:** Amendment'sız gRPC — İlke I NON-NEGOTIABLE olduğu için yasak.

## R3. İç gRPC yetkilendirmesi (İlke V)

- **Decision:** Rezervasyon gRPC çağrıları çağıranın kullanıcı token'ını taşır (Basket/
  Order kendi HTTP context'indeki bearer'ı gRPC metadata'sına iletir). Stock tarafında
  yeni scope `StockReserve = "stock.reserve"` istenir; Identity.Server istemci
  konfigürasyonuna eklenir. Rol getirilmez.
- **Rationale:** İlke V "scope, rol değil" der ve JWT-dışı şemaları da meşru sayar ama
  öz scope-tabanlı yetkidir. Token propagation mevcut bearer altyapısını yeniden kullanır.
- **Alternatives:** Ağ-güveni (Aspire iç ağ) + yetkisiz — İlke V'i zayıflatır. Servis
  hesabı/client-credentials — ekstra kimlik akışı, şimdilik gereksiz.
- **Not:** Anonim sepet kullanıcısının token'ı WebApp BFF'ten gelir (mevcut model); o
  token'a `stock.reserve` scope'u eklenir.

## R4. Rezervasyonun aggregate içinde modellenmesi

- **Decision:** `ProductStock` içinde private `_reservations` listesi (gömülü entity
  `StockReservation { UserId, Quantity, ExpiresAt }`). Alan adı `Quantity` → anlamı
  **OnHand** (fiziksel); hesap `Available = OnHand − Σ aktif (ExpiresAt>now) rezervasyon`.
  Davranışlar: `Reserve(userId, qty, ttl)`, `Release(userId)`, `SetReservedQuantity(userId,
  qty, ttl)` (idempotent set), `Commit(userId)`, `PurgeExpired(now)`. Invariant: Available
  negatif olamaz; Reserve yetersizse `ResultDomain.Error(INSUFFICIENT_STOCK)`.
- **Rationale:** İlke II — tek aggregate root, invariant içeride; çekişme tek dokümanda
  atomik. Marten optimistic concurrency (versiyon) son-ürün yarışını çözer.
- **Alternatives:** Ayrı Reservation aggregate — servis başına tek-root ilkesini bozar,
  atomik Available'ı zorlaştırır. Reservation'ı Basket'te tutmak — çekişme görünmez.
- **Concurrency:** `ProductStock` üzerinde Marten `UseOptimisticConcurrency` (ya da
  numeric revision) açılır; çakışan Reserve'de handler retry/hata döner (çift satış yok).

## R5. TTL temizliği — Hangfire sweep + lazy filtre

- **Decision:** Stock.Api'ye Hangfire eklenir (008 deseni: Postgres `hangfire` şeması,
  `AddHangfire`+`AddHangfireServer`, recurring job). `ReservationSweepJob` periyodik
  (config `Reservations:SweepCron`, vars. dakikalık) süresi geçmiş rezervasyonları
  `PurgeExpired` ile siler ve her biri için `ReservationExpired` yayınlar. Okuma tarafı
  **lazy filtre**: Available hesabı her zaman `ExpiresAt>now` süzer (sweep gecikse de doğru).
- **Rationale:** 008'de Hangfire kanıtlı; lazy filtre görünürlük doğruluğu, sweep fiziksel
  temizlik + event. İki katman tamamlayıcı.
- **Alternatives:** Yalnız lazy — kayıt hiç silinmez, tablo şişer, event çıkmaz. Yalnız
  sweep — sweep arası pencerede yanlış Available.

## R6. "Son N adet" (Available) sayacının UI'a sunumu

- **Decision:** Available, Stock query'sinden **canlı** okunur. `GetStockByProductId`
  yanıtı `OnHand, Reserved, Available` döndürür; WebApp ürün detay + sepet ekranında bunu
  gösterir. Storefront push read-model'i **değiştirilmez** (rezervasyon volatil; her hold'da
  event basmak gürültülü). Storefront listesi mevcut haliyle kalır.
- **Rationale:** Rezervasyon çok sık değişir; push read-model'e taşımak fat-event trafiği
  yaratır. Canlı sorgu, sayaç için yeterince günceldir ve kapsamı dar tutar.
- **Alternatives:** Availability'yi Storefront'a push — 003/006 read-model'ini karmaşıklaştırır,
  YAGNI. Sadece OnHand göstermek — kullanıcıya yanlış "kalan" verir.

## R7. Sepette Quantity + Reserve/Release koordinasyonu

- **Decision:** `BasketItem.Quantity` (int) eklenir. `AddBasketItem` adet artırır (varsa
  +1/verilen); yeni `SetBasketItemQuantity` mutlak adede getirir. Her değişimde Basket,
  Stock'a **idempotent** `SetReservedQuantity(userId, productId, newQty)` gRPC çağrısı
  yapar; başarı → sepet yazılır, başarısızlık (INSUFFICIENT) → işlem reddedilir (fail-closed).
  `DeleteBasketItem` → `Release`. Rezervasyon adedi = sepetteki adet (tek girdi, ayna).
- **Rationale:** İdempotent set, artış/azalışı tek yolla kapsar; ayna model FR-011'i sağlar.
- **Alternatives:** Delta (+/−) çağrıları — çift sayım/yeniden deneme riskine açık.

## R8. Sipariş anında Commit (Order→Stock)

- **Decision:** `CreateOrder` handler'ı, order'ı store etmeden önce her ürün için Stock'a
  `Commit(userId, productId, qty)` gRPC çağrısı yapar: OnHand düşer, rezervasyon kapanır,
  `StockChangedEvent` yayınlanır (Storefront güncel kalır). Commit başarısızsa (rezervasyon
  yok/yetersiz) sipariş **oluşturulmaz** (FR-008). Ödeme zaten alınmışsa refund kapsam dışı.
- **Rationale:** Mevcut akışta sipariş ödeme sonrası "Paid" oluşuyor; Commit burada senkron
  yapılır ki oversell olmasın. Süresi dolan rezervasyon zaten sepetten çıktığı için siparişe
  girmez.
- **Alternatives:** OrderCreatedEvent ile async Commit — sipariş başarılı olur ama stok
  yoksa tutarsızlık/oversell; reddetme yapılamaz.

## R9. Tedarikçi feed'i stoğu ezmez (Model C) — IngestionAgent değişimi

- **Decision:** IngestionAgent'ın `StockWriteExecutor` adımı stok yazmayı bırakır (workflow
  edge'i kaldırılır ya da `ShouldWrite` her zaman false döner; 012 Model C referansıyla).
  İlk seed mevcut `ProductCreatedEvent` → Stock tüketicisi ile yapılır (değişmez). Restock
  explicit: `set_stock`/`IncreaseStock` (manuel/agent).
- **Rationale:** Model C — malı alınca stok bizim; feed OnHand'i ezmemeli. Seed yolu zaten
  ProductCreatedEvent'te olduğundan StockWrite'ı düşürmek seed'i bozmaz.
- **Alternatives:** Feed artışını delta-restock saymak — snapshot mutlak, delta kırılgan;
  kullanıcı reddetti.
- **Not:** 007/005'in "her snapshot'ta SetQuantity" davranışının bilinçli evrimi; ilgili
  spec'lere memory/ADR notu düşülür.

## R10. ReservationExpired event yönlendirmesi (Stock→Basket)

- **Decision:** `Shared.IntegrationEvents`'e `ReservationExpired(ProductId, UserId)` eklenir.
  `RabbitMqConstants`'a fanout exchange + Basket queue eklenir. Stock sweep publish eder;
  Basket `BasketEventHandlers`'ta tüketip ilgili kullanıcının sepetinden o ürün satırını siler.
- **Rationale:** Mevcut fanout event deseni; async temizlik uygundur (bloklamaz). Basket
  OrderCreated'ı zaten benzer şekilde tüketiyor.
- **Alternatives:** Basket'in lazy temizliği — kullanıcı görene dek ölü satır kalır; kullanıcı
  event'i tercih etti.