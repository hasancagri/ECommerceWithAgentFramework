# Research: Checkout Orchestrator (Phase 0)

Tüm spec NEEDS CLARIFICATION'ları `/speckit-clarify` (Session 2026-08-25) ile kapandı; kalan
kararlar teknik desen seçimleridir. Her biri Decision / Rationale / Alternatives ile.

## R1. Adım kanalı: broker komut/yanıt vs gRPC

- **Decision**: Checkout adımları (CreateOrder, Authorize, Commit, Capture, Confirm, ClearBasket +
  telafi) **RabbitMQ üzerinden asenkron komut → yanıt-event** ile yürür. Orchestrator komutu
  yayınlar, hedef BC işler ve `CheckoutId` (korelasyon) taşıyan yanıt-event yayınlar; Wolverine
  saga bunu `[SagaIdentity]` ile eşler.
- **Rationale**: Feature'ın var-oluş sebebi temporal decoupling + at-least-once teslim + outbox/
  inbox derslerini yaşamak (US4/US5). Broker, hedef servis kısa süre düşse bile komutu kuyrukta
  tutar; orchestrator bloke olmaz.
- **Alternatives**: (a) gRPC (028 mevcut) — senkron, temporal coupling; öğrenme hedefini vermez,
  reddedildi. (b) Fanout event-koreografi (saga'sız) — telafi/pivot merkezî kararı modelleyemez,
  reddedildi.

## R2. Yanıt korelasyonu

- **Decision**: Her komut `CheckoutId` (= saga kimliği) + `IdempotencyKey` taşır. Yanıt-event aynı
  `CheckoutId`'yi geri taşır; Wolverine mesajlarda `[SagaIdentity] Guid CheckoutId` ile saga örneğine
  yönlendirir (028'in `OrderId` deseninin birebir eşi).
- **Rationale**: 028 zaten Marten-persisted Wolverine saga'da `[SagaIdentity]` kullanıyor; kanıtlı,
  minimum yenilik.
- **Alternatives**: El-yazımı correlation tablosu — Wolverine yerleşiğini tekrar eder, reddedildi.

## R3. İki-fazlı ödeme durum makinesi (Payment BC)

- **Decision**: Payment aggregate'ine `PaymentState { Authorized, Captured, Voided }` (aggregate
  dosyasında enum) + `Authorize()`, `Capture()`, `Void()` davranış metotları (ResultDomain) eklenir.
  Geçişler guard'lı: Authorize→Captured|Voided; Captured/Voided terminal. Mevcut `PaymentStatus`
  (Success/Failed/Pending) maket akışı bu feature'da checkout için KULLANILMAZ; iki-faz ayrı alan/
  akıştır (eski maket create endpoint'i geriye-uyum için durabilir, checkout onu çağırmaz).
- **Rationale**: FR-005 + İlke II — davranış aggregate'te. FR-015 gereği gerçek PSP hop stub:
  Authorize/Capture/Void lokal olarak durumu değiştirir, dış çağrı yok.
- **Alternatives**: Ödeme durumunu orchestrator DB'sinde tutmak (Q1-B) — anemik gerginliği + BC
  izolasyonu ihlali riski; kullanıcı Q1=A ile reddetti.

## R4. Idempotent consumer / dedup

- **Decision**: İki katman: (1) Wolverine mesaj dedup + inbox (Marten-persisted) tekrar-teslimi
  yakalar; (2) domain düzeyi idempotency — Stock zaten `_processedOps` (orderId:commit/revert) tutar;
  Payment `Authorize/Capture/Void` terminal-guard'la tekrar-uygulamayı yutar; Order `Confirm/Cancel`
  yalnız Pending'den geçer (tekrar no-op). Komut `IdempotencyKey` = `CheckoutId + adım adı`.
- **Rationale**: FR-017/FR-022. Broker at-least-once; çift yan etki olmamalı (SC-006). Domain guard
  + inbox iki hat güvence.
- **Alternatives**: Yalnız inbox — domain guard'ı olmayan Payment'ta çift capture riski; reddedildi.

## R5. Transactional outbox + saga durum atomisitesi

- **Decision**: Wolverine durable outbox + Marten (`IntegrateWithWolverine`, `UseDurableLocalQueues`)
  — saga durum değişimi ile giden mesaj tek transaction'da yazılır (028 emsali).
- **Rationale**: FR-021 — süreç ilerledi ama mesaj gitmedi (veya tersi) durumu oluşamaz. Kanıtlı
  altyapı; ek bileşen yok.
- **Alternatives**: Elle outbox tablosu — Wolverine yerleşiğini tekrar eder, reddedildi.

## R6. Durable timer / watchdog + hata sınıflandırma

- **Decision**: Adım-başına per-step timeout + süreç watchdog `bus.ScheduleAsync(CheckoutTimedOut)`
  (028 deseni). Hatalar iki sınıf: **geçici** (servis erişilemez/timeout) → sınırlı retry + backoff;
  **kalıcı** (iş kuralı reddi / retry tükendi) → pivot öncesi telafi+iptal, pivot sonrası
  log-and-complete. Retry tükenen zehirli mesaj DLQ'ya.
- **Rationale**: FR-023/FR-024/FR-025. 028 watchdog faz-guard mantığı (pivot sonrası no-op) korunur.
- **Alternatives**: Sabit tek timeout — pivot öncesi/sonrası ayrımını modelleyemez, reddedildi.

## R7. Idempotent başlatma (çift checkout)

- **Decision**: `StartCheckout` idempotency anahtarı = `UserId + BasketId` (veya rezervasyon kümesi
  hash'i). Aynı anahtarla ikinci POST yeni saga doğurmaz; mevcut sürecin kimliğini döner.
- **Rationale**: FR-029 / edge "çift checkout". Saga kimliği deterministik türetilir → Marten upsert
  ikinci başlatmayı yutar.
- **Alternatives**: Rastgele saga id + ayrı kilit tablosu — fazladan durum, reddedildi.

## R8. m2m kimlik + scope

- **Decision (düzeltildi)**: Yalnız **giriş endpoint'i** scope-guard: **kullanıcı scope'u
  `checkout.write`** (KnownScopes + `customer` rol demeti; U1 kararı), WebApp BFF token enjekte eder.
  **Broker komut handler'ları `[RequiredScope]` KULLANMAZ**: `ScopeAuthorizationMiddleware` yalnız
  `HttpContext.User`'dan okur; RabbitMQ-tüketilen mesajda HttpContext yok → guard daima throw ederdi.
  Broker trust = RabbitMQ bağlantı auth (Aspire) + tek yayıncı (orchestrator). Orchestrator HTTP/gRPC
  çağırmaz (adımlar broker, kalemler POST gövdesinde) → **OpenIddict m2m client/token GEREKMEZ**
  (028'in SagaTokenHandler'ı gRPC içindi; burada yok).
- **Rationale**: İlke V "her yüzey scope" HTTP/in-proc yüzeyler içindir; broker-internal m2m komut
  kanalının per-mesaj JWT'si mevcut middleware'le teknik olarak imkânsız + gereksiz (tek güvenli
  yayıncı). Kullanıcı yüzeyi (giriş) tam korunur.
- **Alternatives**: Mesaj header'ında m2m token + özel middleware — öğrenme maketi için aşırı; reddedildi.
- **Rationale**: İlke V — makine kimliği client_credentials + statik scope (`order-saga` emsali);
  giriş kullanıcı scope'u (Q3=A).
- **Alternatives**: Broker iç-ağ güvenli varsayımı (Q3-B) — İlke V "her yüzey scope" ihlali,
  reddedildi.

## R9. WebApp giriş noktası taşıması

- **Decision**: `Order/Create` OnPost artık Payment ön-yaratımı + Order POST yapmaz; tek çağrı yeni
  orchestrator `POST /api/v1/checkout` (seçili adres+kart+kalemler). `IsReservationExpired` guard
  POST öncesi WebApp'te KALIR (saga başlamadan sepete döndürür). Yanıt anında "siparişin alındı".
- **Rationale**: FR-003/FR-011/FR-027; mevcut guard + akış gözlemlenebilirliği korunur (Assumptions).
- **Alternatives**: Guard'ı orchestrator'a taşımak — mevcut çalışan WebApp guard'ını gereksiz söker,
  reddedildi (saga-içi yarış zaten FR-011 emniyet ağıyla kaplı).

## R10. 028 söküm kapsamı (full replace)

- **Decision**: Sil: `Order.Api/Sagas/CheckoutSaga.cs`, `Grpc/StockCommitClientProxy.cs`,
  `Grpc/BasketClearClientProxy.cs`, Order'ın saga gRPC istemci wiring'i + `SagaTokenHandler` (Order
  artık downstream gRPC sürmez). `CreateOrder`'dan `StartCheckout` yayını kaldırılır; `CreateOrder`
  broker komut handler'ı olur. `Checkout.WatchdogSeconds` options Order'dan orchestrator'a taşınır.
- **Rationale**: FR-002 — iki süreç aynı anda koşmamalı. Sıfırdan DB (Assumptions) → in-flight taşıma
  yok.
- **Alternatives**: Yan-yana koşum (feature flag) — FR-002 ihlali + çift-işleme riski, reddedildi.