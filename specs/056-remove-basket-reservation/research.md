# Research: 056 Sepet Rezervasyonu Sökümü

## D1 — CommitStock'un yeni anlamı

- **Decision**: `ProductStock.Commit(orderId, quantity)` rezervasyon-çevirme yerine doğrudan düşüm: `Available (OnHand) >= quantity` ise düş, değilse ResultDomain hatası. OrderId-bazlı idempotency defteri (aynı sipariş için ikinci Commit no-op) AYNEN kalır.
- **Rationale**: Saga zaten `StockCommitted(Success, ErrorClass, MessageCode)` yanıtıyla başarısızlığı taşıyor (CheckoutMessages.cs:48) — orkestratöre sıfır dokunuş. Yeterlilik invariant'ı aggregate içinde (İLKE II).
- **Alternatives considered**: Checkout-kapsamlı kısa TTL rezervasyon (checkout başında tut, sonda çevir) — yeni durum + süre geri gelir, sökümün amacını boşar; reddedildi.

## D2 — Uçuştaki SweepReservation zamanlanmış mesajları

- **Decision**: Mesaj tipi + handler tamamen silinir. Deploy sonrası Wolverine durable kuyruğundaki eski `SweepReservation` zarfları tip çözülemeyince dead-letter/hata kuyruğuna düşer; dev ortamda kabul edilebilir gürültü.
- **Rationale**: Dev-tek-ortam, DB sık sıfırlanıyor; bir sürüm no-op handler taşımak ölü kod bırakır ([[wolverine-eventhandler-includetype]] tuzağına da bulaşmaz).
- **Alternatives considered**: Bir sürüm no-op handler (kademeli söküm) — prod olsaydı doğruydu; dev'de gereksiz.

## D3 — stock_reservation.proto kaderi

- **Decision**: Proto dosyası + iki csproj `Protobuf` item'ı + `StockReservationGrpcService` kaydı silinir. gRPC Commit/RevertCommit uçları da gider — saga 049'dan beri broker komut/yanıt kullanıyor (CheckoutMessages → StockCommandsQueue).
- **Rationale**: Grep doğrulaması: proto'yu yalnız Basket.Api (Client) + Stock.Api (Server) referanslıyor; Order.Api'de yalnız bayat obj/ çıktıları var. Ölü kanal bırakmak İLKE I kanal disiplinini bulandırır.
- **Alternatives considered**: Proto'da yalnız reserve/release silip Commit/RevertCommit gRPC bırakmak — çağıranı yok, ölü yüzey; reddedildi.

## D4 — Sepete eklemede stok ön-kontrolü

- **Decision**: Yok. Sepete ekleme stok sormaz; tek sınır kalem başına 5 tavanı (Basket aggregate'inde). Stok görünürlüğü ürün sayfasında (StorefrontView) sürer.
- **Rationale**: Kitapyurdu modeli; soft-check eklemek Basket→Stock bağımlılığını (sökmek istediğimiz şeyi) başka biçimde geri getirir.
- **Alternatives considered**: StorefrontView stok snapshot'ına danışan soft-check — bayat veriyle yanlış ret riski + BC bağımlılığı; reddedildi.

## D5 — Eski veri kalıntıları (FR-009)

- **Decision**: Ek iş yok. Marten/Newtonsoft, dokümandaki artık alanları (Basket.ReservationExpiresAt, ProductStock.Reservations) sınıfta karşılığı kalmayınca sessizce yok sayar.
- **Rationale**: Non-public setter + ctor bağlama mevcut kurulumda ekstra alanı tolere eder; dev DB'ler zaten sık sıfırlanıyor.
- **Alternatives considered**: Patch migration ile alan temizliği — değer katmıyor; reddedildi.

## D6 — WebApp sayaç zinciri

- **Decision**: BasketCountdown ViewComponent + `GetCountdownAsync` + `PurgeExpiredBasketAsync` + `/purge-expired` endpoint zinciri komple silinir. 021 adet tavanı UI'ı (min 5) sabit 5'e sadeleşir (stok bileşeni yoktu zaten UI'da ayrı kaynaktan geliyorsa kaldırılır).
- **Rationale**: Süre kavramı kalkınca zincirin her halkası ölü; yarım bırakmak "sepetim neden boşaldı" sürprizine döner (C seçeneği reddi).