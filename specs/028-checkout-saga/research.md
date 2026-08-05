# Research: Checkout Saga (028)

## R1 — Saga motoru: Wolverine durable Saga + Marten

- Decision: `Wolverine.Saga` sınıfı, Order BC içinde; state Marten belgesi olarak persist (IntegrateWithWolverine mevcut).
- Rationale: altyapı hazır (Program.cs:18); durable local queue + scheduled message 026'da kanıtlandı; ek bağımlılık yok.
- Alternatives: elle state makinesi (saga tablosu + handler'lar) — Wolverine'in verdiğini yeniden yazmak; MassTransit — yeni bağımlılık, reddedildi.

## R2 — Adım ilerleyişi: saga-içi mesajlar, kalem başına tek mesaj

- Decision: `StartCheckout` saga'yı başlatır; `CommitNextItem` her seferinde TEK kalemi gRPC ile commit eder, sonra kendini yeniden gönderir.
- Başarısızlıkta `CompensateCheckout` commit edilenleri tersine çevirir; başarıda `ClearBasketStep` koşar. Tüm mesajlar durable local queue'da.
- Rationale: kalem başına mesaj = crash noktası ne olursa olsun kayıp adım tek kalem; state'te ilerleme (`NextIndex`, `CommittedItems`) net.
- Alternatives: tek handler'da for-döngüsü (bugünkü şekil) — crash'te hangi kalemin commit edildiği bilinmez; reddedildi.

## R3 — Watchdog: scheduled message (026 deseni)

- Decision: saga start'ta `CheckoutTimedOut(OrderId)` mesajı `ScheduleAsync` ile kurulur; süre `Checkout:WatchdogSeconds` config (varsayılan 120).
- Fire anında saga bitmişse no-op (Wolverine tamamlanmış saga'ya mesajı düşürür); bitmemişse telafi + `ORDER_TIMEOUT` iptali.
- Rationale: Postgres'te kalıcı zarf; restart'a dayanır (FR-011/012/014). SweepReservation ile aynı, kanıtlanmış mekanizma.

## R4 — Arka plan yetkisi: client credentials makine token'ı

- Decision: saga'nın gRPC çağrıları (Stock Commit/Revert, Basket Clear) kullanıcı bearer'ı DEĞİL, Duende client-credentials token'ı taşır.
  Yeni Identity client `order-saga` (scope: `stock.reserve`, `basket.write`); Order.Api token'ı alıp cache'leyen bir delegating handler kullanır.
- Rationale: saga arka planda koşar, HttpContext yok; `BearerForwardingHandler` çalışamaz. Kullanıcı token'ını saga state'e koymak
  restart-sonrası expiry'de telafiyi kırar (uzun kesintide RevertCommit 401 alır) — reddedildi.
- Not: kullanıcı kimliği yetki için değil, veri olarak taşınır (UserId komut gövdesinde; bugünkü gRPC sözleşmesiyle aynı).

## R5 — Commit/Revert idempotency: order_id operasyon anahtarı

- Decision: proto `Commit` ve yeni `RevertCommit` mesajlarına `order_id` eklenir. `ProductStock` işlenmiş operasyon anahtarlarını
  (`orderId` + yön) küçük bir listede tutar; mükerrer Commit/Revert no-op başarı döner.
- Rationale: at-least-once teslimat → crash sonrası aynı adım yeniden koşabilir. Anahtarsız Commit tekrarı `NO_ACTIVE_RESERVATION` döner ve
  "zaten yapıldı" ile "rezervasyon yok" ayrıştırılamaz; yanlış telafi tetiklenir. FR-007'nin Stock tarafındaki karşılığı.
- Alternatives: "in-flight bayrağı + NO_ACTIVE_RESERVATION'ı başarı say" — süresi dolmuş rezervasyonla karışır, stok kaçağı riski; reddedildi.

## R6 — Retry sınıflandırması ve sayaçlar

- Decision: gRPC proxy'ler Result döner (exception yutulur, 012 deseni). İş hatası (INSUFFICIENT/NOT_FOUND) retry EDİLMEZ → telafi.
  Teknik hata (UNAVAILABLE kodu) saga state'teki `Attempt` sayacıyla en çok 3 kez, 5 sn arayla scheduled re-dispatch edilir.
- Rationale: retry kararı iş kuralı; Wolverine exception-policy'si Result tabanlı akışı göremez. Sayaç state'te = restart'a dayanıklı.

## R7 — ClearBasket kanalı: Basket'e gRPC sunucu ucu

- Decision: yeni `basket_clear.proto`; Basket.Api gRPC Server olur (AddGrpc + MapGrpcService — Stock'taki desenin aynısı).
  İnce sarmalayıcı yeni `ClearBasketByCheckout` Wolverine command'ini çağırır (sepet yoksa Ok; idempotent). Scope: `basket.write`.
- Rationale: tam orchestration kullanıcı kararı; event zinciri kalkıyor (FR-015). Yeni scope açmak yerine mevcut `basket.write` yeter.

## R8 — OrderStatus evrimi: int değerler korunur

- Decision: `WaitingForPayment=1 → Pending=1`, `Paid=2 → Confirmed=2`, `Cancel=3 → Cancelled=3`. Yeni alan `CancelReason` (resource kodu).
- Rationale: Newtonsoft enum'u int yazar; değerler sabit kalınca eski dev kayıtları migration'sız okunur. Anayasadaki `Enumeration` tercihi
  mevcut enum'u değiştirmeyi zorunlu kılmaz (mevcut kod enum; dönüştürme ayrı refactor, kapsam dışı).

## R9 — Anayasa İlke I amendment

- Decision: İlke I'deki gRPC sanksiyonu "anlık evet/hayır kararı" ifadesinden "anlık karar VE orkestre edilmiş saga adım komutları"na
  genişletilir (MINOR, v1.4.0). Gerekçe: 028 tam-orchestration kararı; DB izolasyonu değişmez, kontratlar `Shared/Protos`'ta kalır.
- Rationale: governance kuralı — çelişen plan ya koda uydurulur ya anayasa amendment'lanır; kullanıcı orchestration'ı bilinçli seçti.

## R10 — Silinenler

- `IntegrationEvents.OrderCreatedEvent`, Order Program.cs'teki exchange/publish tanımı, `RabbitMqConstants.OrderCreated`,
  Basket'teki `OrderCreatedEvent` handler'ı. `ReservationExpired` ve tüketicisi AYNEN kalır (saga dışı, TTL mekanizması).
