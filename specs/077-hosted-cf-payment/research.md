# Research: Hosted-CF Ödeme (077)

Faz 0 — bilinmeyenleri çöz + desen doğrula. Kod ankraj'ları investigator haritasından (2026-09-13).

## D1 — Order→Payment senkron kanalı (link isteği)

- **Karar:** Order.Api → Payment.Api **internal S2S** (HTTP REST, sanctioned RPC). `start_payment` hosted URL'yi tool sonucu olarak anlık döndürmeli → async event uymaz; senkron RPC İLKE I'de sanctioned.
- **Gerekçe:** URL geri dönüşü zorunlu (kullanıcı-yüzü). `PaymentIntent` Payment BC'de kalır; Order onun DB'sine değmez, ince kontrat üzerinden çağırır.
- **Alternatif red:** broker komut/yanıt — yanıtı senkron toplamak için ek korelasyon/bekleme; agent tool'unda gecikme + karmaşıklık. gRPC — REST yeterli, mevcut internal-REST deseni (GetMerchantKeyInternal) var.

## D2 — Payment→PG hosted-payment çağrısı + MerchantKey

- **Karar:** Payment.Api'de yeni `PgHostedPaymentClient` (HttpClient). MerchantKey'i Customer.Api `GetMerchantKeyInternal` (S2S, customer.read) ile per-request alır (mevcut desen: `payment-gateway-merchant-key-single-source`); PG base URL `PaymentOptions.PgBaseUrl` (Options).
- **Gerekçe:** MerchantKey tek kaynak = Customer MerchantInformation; statik config reset sonrası 401 tuzağı (memory). Per-request çekilir.
- **Açık (tasks'ta):** MerchantKey çağrısını Order.Api mı Payment.Api mı yapsın. **Karar:** Payment.Api (PG çağrısını o yapıyor, key'i o taşımalı; Order sadece "link üret" der). Order→Payment S2S body'sinde MerchantKey TAŞINMAZ.
- **Alternatif red:** MerchantKey'i statik config'e koymak — reset sonrası bayatlar (bilinen tuzak).

## D3 — Callback dayanıklılığı (inbox-outbox)

- **Karar:** Callback HTTP ucu ince; gövde doğrulanınca tek Wolverine command'a (`HandlePaymentCallback`) devreder. Handler `[Transactional]`: `PaymentIntent` durum yaz + `PaymentSucceeded`/`PaymentFailed` **aynı transaction'da** yayınla (Marten+Wolverine durable outbox; `IntegrateWithWolverine()` zaten var, `AutoApplyTransactions`/`UseDurableLocalQueues` mevcut).
- **Gerekçe:** Commit olmazsa event de yok → PG retry tekrar dener; commit olursa event outbox'ta, çökme sonrası restart'ta yayınlanır (kayıp yok, FR-009).
- **İdempotency:** `TxRef` Marten unique index (`Duplicate`/`UniqueIndex`) + handler başı `Status != Pending → return` guard. Çift callback no-op (FR-008). Downstream saga/stock zaten broker dedupe.
- **Alternatif red:** ayrı outbox tablo/framework — Wolverine+Marten zaten sağlar.

## D4 — Terk tespiti (expiry)

- **Karar:** `PaymentIntent` create anında `bus.ScheduleAsync(new PaymentIntentExpiryCheck(TxRef), TimeSpan.FromSeconds(PaymentOptions.IntentTimeoutSeconds))` (varsayılan 300). Handler `Process/PaymentIntentExpiry`: intent yükle; `Status == Pending` → `Expire()` + `PaymentFailed(…, "ABANDONED")`; değilse no-op.
- **Gerekçe:** Checkout watchdog (`CheckoutTimedOut` + `ScheduleAsync`) birebir emsal; Marten-backed dayanıklı, restart'a dayanır. Aktif poll değil (poll backlog).
- **Yarış:** callback vs timer → `Status` guard; hangisi önce commit ederse kazanır, diğeri no-op.

## D5 — Callback imza doğrulama (HMAC)

- **Karar:** `X-Signature: HMAC-SHA256(CallbackSecret, raw_body)` header; store aynı hesabı yapar, sabit-zamanlı karşılaştırır; uyumsuz/eksik → 401, işlem yok. `CallbackSecret` = MerchantKey'den AYRI (`PaymentOptions.CallbackSecret`, onboarding'de dağıtılır).
- **Gerekçe:** Callback açık internet ucu; sahte "ödendi" POST'unu keser (US3). Ayrı secret = sızıntı yalıtımı (Q2).
- **Uygulama:** minimal API filter / middleware raw body okur (buffering). Not: raw body imza için gövde-değişmez okunmalı.
- **Alternatif red:** token/scope (Q2'de reddedildi — server webhook, oturum yok).

## D6 — PaymentSucceeded/PaymentFailed event yeri + tüketim

- **Karar:** `Shared/IntegrationEvents.cs`'e fanout event olarak eklenir (OrderCompleted/ProductChangedEvent emsali; yayıncı exchange declare, **tüketici binding kurar**). Order.Api tüketir.
- **Order.Api tüketici:** `PaymentSucceeded` → Pending order oku → `StartCheckout(AlreadyCaptured, OrderId, Items, Amount, Address, UserId)` yayınla (mevcut StartQueue publish route var). `PaymentFailed` → `Order.Cancel` (mevcut Cancel davranışı; saga'ya girmeden doğrudan, çünkü stok hiç düşmedi).
- **Gerekçe:** Additive fanout; eski tüketici kırılmaz. Order zaten StartCheckout yayıncısı (049 chat yolu emsali).
- **Alternatif red:** Payment doğrudan StartCheckout yayınlasın — StartCheckout Items/Address ister; onlar Order'da. Order tüketip kendi verisinden kurar (temiz).

## D7 — start_payment order oluşturma + AlreadyCaptured OrderId

- **Karar:** `start_payment` (Order.Api agent slice) Order'ı **Pending** oluşturur (mevcut `Order.Create` + `OrderStatus.Pending`); saga'nın eski `CreateOrderCommand` yolu (Charge modu) AlreadyCaptured'da kullanılmaz. Order.Create bugün Saga/OrderEventHandlers'ta çağrılıyor; start_payment kendi handler'ında Domains davranışını (Order.Create) çağırır (İLKE III: kullanıcı isteği → Domains/Features/Agents).
- **Re-use (Q1/A2):** start_payment önce aynı UserId+sepet için **canlı Pending PaymentIntent** var mı bakar (Payment.Api'ye sorar / intent'te UserId+basket-hash) → varsa mevcut HostedUrl döner. Bayat → eski Expire + taze. Ayrıntı data-model.
- **Basket okuma:** checkout gRPC `BasketQuery.GetBasketItems(UserId)` → items + total_price. Boşsa (FR-018) dostça Result mesajı, order/intent yok.

## D8 — Söküm kapsamı (Charge modu)

- **Silinecek:** `Payment.cs` (mock aggregate: Create/SetStatus/Charge + PaymentStatus), `PaymentEventHandlers.cs`, Payment.Api Program.cs `PaymentCommandsQueue` listen + `PaymentCharged` publish + `IncludeType(PaymentEventHandlers)`; `CheckoutMessages` `ChargePaymentCommand`/`PaymentCharged`; saga `PaymentMode.Charge` dalı (`Start` web yolu + `Handle(StockCommitted)` charge dalı) + `Handle(PaymentCharged)` + `Charging` fazı + `CHECKOUT_PAYMENT_CHARGE_FAILED` (saga artık pivot öncesi telafi görmez? — hayır: AlreadyCaptured'da charge yok, pivot dışarıda). `PaymentResourceConstants` charge kodları.
- **Kalır:** `PaymentMode.AlreadyCaptured` + saga CommitStock→Confirm→ClearBasket + telafi (RevertCommitStock/CancelOrder) + watchdog. `StartCheckout` (CardRef/Installments alanları sadeleşir/kalkar — kullanılmıyor).
- **Guard:** `scripts/check-flow-links.sh` — FLOW.md'den silinen tip adları (Payment.Charge, ChargePaymentCommand, PaymentCharged) çıkarılmalı yoksa drift hatası.

## D9 — PaymentOptions (config)

- **Karar:** `Options/PaymentOptions.cs` POCO — `IntentTimeoutSeconds` (300), `CallbackSecret` (string), `PgBaseUrl` (string). Bağlama: `AddOptions<PaymentOptions>().BindConfiguration(nameof(PaymentOptions)).ValidateDataAnnotations().ValidateOnStart()` (SmtpOptions/CheckoutOptions emsali). Tüketici düz `PaymentOptions` enjekte eder (İLKE: IOptions değil).
- **Secret kaynağı:** dev = user-secrets (`PaymentOptions:CallbackSecret`); iyzico key gibi fail-fast ValidateOnStart.