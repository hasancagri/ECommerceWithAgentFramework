# Research: Personalization Signal Store (Faz 1)

Phase 0 — teknik bilinmeyenlerin çözümü. Desenler mevcut kod tabanından (Reviews.Api,
Stock.Api, Storefront.Api, CheckoutSaga, WebApp) çıkarıldı.

## D1 — Gezinme kanalı: HTTP mü, dosya mı, event mi?

- **Karar**: WebApp (BFF) → Personalization.Api **doğrudan HTTP POST** (batch), arka
  plan kuyruğundan (mevcut `BehaviorLogWriter` deseni; çıkış `File.Append` yerine
  `httpClient.PostAsync`).
- **Gerekçe**: Kullanıcı doğrudan istek istedi; tek sink (birleşme); WebApp→servis HTTP
  zaten norm (Refit + service discovery). Kayıp-toleransı client tarafında korunur
  (bounded channel + `DropWrite`, sayfa bloklanmaz).
- **Anayasa**: İlke I telemetri istisnası (v1.9.0) telemetriyi UI/BFF→tek-tüketici BC
  olarak meşrulaştırır; biçimi (dosya) HTTP'ye genişletildi — BFF→servis çağrısı cross-BC
  değil. İkinci tüketici doğarsa integration event'e terfi.
- **Alternatifler**: (a) Dosya-kanalı (042) — ikinci paralel mekanizma + Python bağı,
  reddedildi. (b) Integration event per görüntüleme — yüksek hacim RabbitMQ selini +
  gereksiz garanti, reddedildi (telemetri kayıpsızlık istemez).

## D2 — Satın-alma tetiği + yeni event

- **Karar**: Order BC yeni `OrderCompleted` integration event yayınlar; tetik =
  **CheckoutSaga başarı** noktası (`CheckoutSaga` → `MarkCompleted()`, ödeme onaylı +
  stok commit + basket temizlendikten sonra). Personalization `*EventHandlers` tüketir.
- **Gerekçe**: Clarify Q1 — yalnız gerçek/ödenmiş satın-alma sinyal olsun. Saga başarısı
  = ödeme onaylı + stok commit; en doğru "purchase happened" anı.
- **Bulgu**: Order şu an completion'da **hiç** integration event yaymıyor (028'de
  `OrderCreatedEvent` bilinçli silinmişti). Yeni event sıfırdan eklenir.
- **Konum**: `CheckoutSaga` içinde `MarkCompleted()` çağrısının olduğu yerde
  `bus.PublishAsync(new OrderCompleted(...))` (saga zaten Order state + items'a erişir).
- **Alternatif**: Order.Confirmed state projeksiyonu — reddedildi (Confirmed pivot
  ödeme öncesi/sonrası netliği saga'da daha temiz).

## D3 — Event yükü: Category/Brand var mı?

- **Karar**: `OrderCompleted` kalemleri Order'ın SAHİP OLDUĞU alanları taşır: ProductId,
  Quantity, UnitPrice (+ varsa ProductName). **Category/Brand event'te nullable**;
  Order bunları tutmuyorsa `null` gider.
- **Gerekçe**: BC izolasyonu — Order, Catalog'un kategori/marka modelini bilmez. Spec
  edge-case zaten "kategori/marka yoksa boş bırak, satın-almayı kaybetme" diyor.
- **İleride**: Kategori/marka zenginleştirmesi (ProductId→Storefront join) sonraki faz;
  Faz 1 event ne veriyorsa onu saklar.
- **Alternatif**: Personalization'ın Storefront'a senkron enrichment çağrısı — kapsam
  dışı + gereksiz coupling, reddedildi.

## D4 — BehaviorSignal modeli (İlke II gerilimi)

- **Karar**: `BehaviorSignal` = **telemetri Marten document** (AggregateRoot DEĞİL);
  statik `Create` fabrikası minimal doğrulama yapar (bilinen EventType, zorunlu kimlik
  alanları) ve `ResultDomain` döner. Write-once; mutasyon yok.
- **Gerekçe**: Domain-gerçeği değil, telemetri (İlke I v1.9.0). Anemik aggregate açmak
  İlke II ihlali olurdu. Conventions: read-model/non-aggregate BC'de ayrı yerleşebilir.
- **PurchaseSignal** aksine gerçek domain-gerçeği + invariant → AggregateRoot (D5).

## D5 — PurchaseSignal aggregate + idempotency

- **Karar**: `PurchaseSignal : AggregateRoot`, Id = OrderId (idempotent doğal anahtar).
  `Create(orderId, userId, orderedAt, items)` invariant'ları korur: en az 1 kalem, her
  kalem adet>0 + tutar≥0. Kalemler private koleksiyon, `IReadOnlyList` expose.
- **Idempotency**: Handler önce `session.LoadAsync<PurchaseSignal>(orderId)` — varsa
  no-op (mükerrer event teslimi güvenli). Id=OrderId olduğundan tekrar `Store` da upsert;
  yine de erken-return ile net idempotency.
- **Gerekçe**: FR-005 idempotent; Marten Id doğal anahtar; İlke II invariant aggregate'te.
- **Test-first (İlke VI)**: Create invariant'ları + VO'lar.

## D6 — Ingest endpoint yetkilendirme (İlke V)

- **Karar**: `POST /v1/signals` **statik scope** `personalization.ingest` ile
  `.RequireAuthorization`. WebApp bu scope'u **client_credentials makine token'ıyla**
  sunar (yeni OIDC client `webapp-personalization` veya mevcut makine kimliği + statik
  scope). Son-kullanıcı kimliği payload'da (userId?/anonymousId/sessionId).
- **Gerekçe**: Gezinme anonim son-kullanıcıyı kapsar → user token garanti değil; ama
  İlke V "her yüzey scope-gated". Makine kimliği = client_credentials + statik scope
  (İlke V). Açık anonim yazma endpoint'i (abuse) önlenir.
- **Scope registry**: `personalization.ingest` `KnownScopes`'a eklenir (kapalı registry).
- **Alternatif**: Anonim-açık + internal-only network — İlke V "her yüzey scope" ile
  zayıf; reddedildi.

## D7 — Aspire wiring + isim çakışması

- **Karar**: Resource `personalization-api`, DB `personalizationApiDb`. Python resource
  `personalization` + `personalizationDb` aynen kalır. AppHost: `postgres.AddDatabase(
  "personalizationApiDb")` + `AddProject<Projects.Personalization_Api>("personalization-api")`
  `.WithReference(db).WithReference(rabbit).WaitFor(...)`. Tüketici binding dersi (007):
  Personalization RabbitMQ exchange/queue binding'ini KENDİ Program.cs'inde kurar +
  `.WaitFor` sırası doğru verilir.
- **Gerekçe**: Python `personalization` ile ad çakışmaz; peer adlandırma (`reviews-api`).

## D8 — Wolverine tüketici tuzağı

- **Karar**: `opts.Discovery.IncludeType(typeof(PersonalizationEventHandlers))` Program.cs'e
  eklenir. Exchange fanout declare + `BindQueue` + `ListenToRabbitQueue`.
- **Gerekçe**: `*EventHandlers` çoğul adı auto-discovery'de atlanır (memory + Reviews
  örneği). `DurabilityMode.Solo` (dev), `UseDurableLocalQueues`, `AutoProvision`.

## D9 — WebApp gönderim: batch + non-blocking + kayıp-toleranslı

- **Karar**: `BehaviorLogWriter` kuyruğu korunur; arka plan işçi kuyruktan N kayıt
  toplayıp tek `POST /v1/signals` (liste) atar. Personalization erişilemezse: kısa retry,
  sonra **drop** (gezinme kayıp-toleranslı). Sayfa hiç beklemez (Enqueue O(1)).
- **Gerekçe**: SC-002 (sayfa gecikmesi yok) + SC-003 (servis kapalı → alışveriş bozulmaz).
  Batch HTTP overhead'i düşürür.
- **Not**: Mevcut dosya yazımı bu akış için emekli; `BehaviorEvent` record'u (zaten
  `Channel/SearchTerm/ShownProductIds/SchemaVersion` içerir) gövde olarak yeniden kullanılır.

## D10 — Retention / hacim

- **Karar**: Bu fazda retention politikası YOK (uzun tut). Ölçek demo.
- **Gerekçe**: Düşük etki (clarify Outstanding); RFM/model uzun geçmiş ister. Politika
  gerekince sonraki fazda (partition/TTL) ele alınır.