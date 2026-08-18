# Phase 0 Research: Chat Order Completion (039)

Amaç: Yol 2 (sunucu orkestrasyonu) kararı sonrası kalan tasarım bilinmezlerini çözmek.

## R1 — Orkestrasyon sahibi: PaymentAttempt saga nerede yaşar?

- **Decision**: Order.Api'de yeni **`PaymentAttemptSaga`** (Wolverine durable, Marten state,
  Id = correlation-key). Charge + reconcile'ı yürütür; başarıda Order oluşturur → mevcut
  `CheckoutSaga` (028) tetiklenir. İki saga zinciri: PaymentAttempt (para) → Checkout (fulfillment).
- **Rationale**: CheckoutSaga zaten Order.Api'de; yeni akış ona besleniyor. Order BC Marten+Wolverine
  durable'a sahip. "Saga sürecin sahibi BC'de host edilir; ayrı orchestration servisi açılmaz" (028).
  Stateless ChatAgent durable saga tutamaz.
- **Alternatives**: (a) Payment.Api owns — BC-temiz ama Payment→Order handoff + Payment.Api bugün
  neredeyse kullanılmıyor, çekim PG'de; (b) tek birleşik saga — charge Confirm'den önce olduğu için
  028'in "order Pending doğar" modelini bozar. İki ayrı saga daha temiz sınır.

## R2 — Correlation-key: türetim + çekime enjeksiyon

- **Decision**: **Deterministik + HMAC** anahtar, sunucuda türetilir:
  `HMAC(serverSecret, userId + basketId + basketContentHash + installment)`. `place_order` handler'ı
  türetir, PaymentAttemptSaga Id'si olur, PG çekim isteğine **sunucu koyar** (LLM görmez/vermez). Aynı
  sepet+taksit için aynı anahtar → doğal idempotency (retry çift çekmez). HMAC + userId içermesi anahtarı
  **forge edilemez + sahiplik-taşıyıcı** yapar (bkz R5) — girdiler bilinse bile secret olmadan üretilemez.
- **Rationale**: Deterministik olması, kayıp-yanıtta sunucunun anahtarı **yeniden hesaplayıp** PG'yi
  sorgulamasını sağlar (id saklamaya bağımlı değil). basketContentHash sepet değişirse anahtar değişir
  (yeni çekim doğru). FR-015/016/017.
- **Alternatives**: Rastgele GUID — kaybolursa yeniden üretilemez, kurtarma zorlaşır. LLM-üretimi —
  güvensiz, reddedildi.
- **Open**: basketContentHash tanımı (ürün+adet+fiyat sıralı). data-model'de netleşir.

## R3 — Sepet kalemleri: Order.Api nasıl okur?

- **Decision**: Basket'e **yeni gRPC `GetBasketItems`** RPC (`Shared/Protos`). Order.Api istemci;
  Basket sunucu (mevcut `BasketClearGrpcService` yanında). Döner: kalem başına ProductId, Name,
  UnitPrice, Quantity (+ TotalPrice). İnce sarmalayıcı — `GetBasket` query'sini `IMessageBus` ile çağırır.
- **Rationale**: Bugün yalnız `ClearBasket` gRPC + REST GET var; REST'i sunucudan tüketmek yerine
  gRPC saga'nın mevcut Basket kanalıyla tutarlı (İlke I sanksiyonlu). Kalem sunucu-otoritesi (FR-004);
  LLM'e girmez. Fiyat manipülasyonu imkansız.
- **Alternatives**: Order REST GET /baskets — çalışır ama gRPC deseni zaten var, kontrat netliği düşük;
  StartCheckout'a kalemi client'tan almak (bugünkü WebApp) — chat'te client yok, LLM'e emanet olur ✗.

## R4 — Ödeme bağlamı (buyer + vaultToken + adres): yapısal kanal

- **Decision**: `get_payment_context` mantığına **yapısal ikiz** (gRPC tercih, Order↔Basket ile aynı
  desen). Customer.Api mevcut `GetPaymentContextForAgent` handler'ını yapısal uçtan da sunar
  (`PaymentContextView`: MerchantId, VaultToken, Card*, Buyer* alanları). Order.Api makine kimliğiyle çağırır.
- **Rationale**: Order.Api agent değil → MCP `get_payment_context`'i imperatif çağıramaz (İlke I).
  Aynı veriyi yapısal kanaldan alır. Buyer VERBATIM siparişe + PG çekimine gider (FR-005).
- **Alternatives**: place_order payload'ında buyer'ı LLM'den almak — VERBATIM garantisi yok, güvensiz ✗.
- **Open**: Adres alanları `PaymentContextView`'da düz (BuyerCity/Country/RegistrationAddress);
  Order `AddressDto`'ya eşleme data-model'de.

## R5 — PG yapısal çekim + verify kontratı (dış bağımlılık)

- **Decision**: PG (ayrı repo) iki yapısal uç açar: (1) **charge** — vaultToken + buyer + amount
  (price/paidPrice) + **correlationKey** alır, idempotent (aynı key → var olan ödeme), döner:
  paymentId + status + amounts + buyer; (2) **retrieve** — paymentId **veya** correlationKey ile
  status+amount+buyer döndürür (verify + reconcile). Auth: merchant API key.
- **Rationale**: Yol 2 çekimi yapısala taşıdı; verify/reconcile aynı retrieve ucunu kullanır. Charge
  zaten PG'de persist ediliyor (Payment agg) — eklenen: correlationKey alanı + idempotent dedupe +
  okuma ucu (+ buyer referansı).
- **Alternatives**: A2A charge (İlke I ihlali agent-olmayan Order.Api için ✗); webhook-only teyit —
  reconcile'ı karmaşıklaştırır, pull daha basit.
- **Sahiplik — ÇÖZÜLDÜ (F1)**: PG buyer persist ETMİYOR (teyit: yalnız MerchantId+VaultToken+tutar+
  status; correlation/externalRef de yok). Ayrı buyer alanı **gerekmez** — sahiplik **HMAC
  correlation-key**'te taşınır: Order.Api ödemeyi yalnız çağıranın userId'sinden yeniden hesapladığı
  anahtarla retrieve eder; başka kullanıcı anahtarı üretemez (farklı userId + secret). PG yalnız
  anahtarı persist+indeks eder (idempotency/reconcile için nasılsa gerekli). Ek kat: vaultToken zaten
  yalnız caller'ın kartlarından gelir (Customer context).

## R6 — Reconcile mekaniği: durable, sınırlı, iki tetikleyici

- **Decision**: PaymentAttemptSaga içinde Wolverine **`ScheduleAsync`** ile self-scheduled
  `ReconcileTick(correlationKey)`; her tick PG retrieve → success/failed/pending. Pending/erişilemez →
  backoff'lu yeniden schedule (ör. 5s,15s,60s...), **deadline** (config) dolunca terminal
  `needs-reconciliation`. Ön plan: kullanıcı chat'te tekrar sorunca `place_order` aynı saga'yı bulur
  (Id=correlation-key) ve **hemen** bir tick koşar. İki yol tek idempotent `OnReconcileTick`'e iner.
- **Rationale**: 028 watchdog / 026 `ScheduleAsync` deseni; yeni altyapı yok. `On*` saf → test-first.
  Deadline sonsuz döngüyü önler; terminal durum ops görünürlüğü (FR-018/019/020).
- **Alternatives**: Hangfire cron sweep — 026'da terse çevrildi (durable scheduled tercih); harici
  poller — durable değil, restart'ta kaybolur ✗.

## R7 — Idempotency katmanları (çift çekim / çift sipariş)

- **Decision**: İki ayrı anahtar: (1) **correlation-key** → PG çekim dedupe + saga tekliği (Id);
  (2) **paymentId** → Order oluşturma dedupe (mevcut `CreateOrder` idempotency check, FR-007).
  PaymentAttemptSaga Id=correlation-key olduğundan aynı sepet için ikinci `place_order` yeni saga
  başlatmaz, var olanı ilerletir.
- **Rationale**: Çekim ve sipariş farklı sınırlar; her biri kendi idempotency anahtarı. At-least-once
  teslimatta yakınsar (028 deseni).
- **Open**: Yok — mevcut Order idempotency check korunur, correlation-key katmanı eklenir.