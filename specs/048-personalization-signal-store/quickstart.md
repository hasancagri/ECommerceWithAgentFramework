# Quickstart / Validation: Personalization Signal Store (Faz 1)

Uçtan uca doğrulama rehberi. Detaylar `contracts/` + `data-model.md`'de; burada
yalnız çalıştırma + beklenen sonuç.

## Önkoşullar

- Docker (Postgres + RabbitMQ), .NET 10 SDK.
- Sistem **Aspire AppHost'tan** başlatılır (tek servis değil):
  `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Marten şeması açılışta otomatik (`ApplyAllDatabaseChangesOnStartup`) — migration yok.
- `personalization-api` + `personalizationApiDb` AppHost dashboard'da sağlıklı görünür;
  RabbitMQ `order.completed` exchange + `personalization.order-completed` queue bağlı.

## Senaryo 1 — Satın-alma sinyali (US1, P1, kayıpsız)

1. Customer login → sepete ürün ekle → checkout → siparişi **ödemeyle tamamla**
   (CheckoutSaga başarı).
2. Beklenen: Order `OrderCompleted` yayar; Personalization tüketir.
3. Doğrula: `personalizationApiDb`'de `PurchaseSignal` (Id=OrderId) var; kalemler
   ProductId/Quantity/UnitPrice içeriyor (Category/Brand null olabilir).
4. **Idempotency**: aynı event yeniden teslim (RabbitMQ redelivery / manuel) → tek kayıt
   kalır, mükerrer yok.
5. **Kayıpsızlık**: `personalization-api`'yi durdur → sipariş tamamla → servisi başlat →
   sinyal kurtarma sonrası yazılır (durable queue).

## Senaryo 2 — Gezinme sinyali (US2, P2, kayıp-toleranslı)

1. Anonim (veya login) kullanıcı bir ürün detayını aç, listeye bak, sepete ekle.
2. Beklenen: WebApp arka plan işçisi batch `POST /v1/signals` atar (scope
   `personalization.ingest`); sayfa gecikmesiz render olur.
3. Doğrula: `personalizationApiDb`'de `BehaviorSignal` kayıtları (yalnız ProductViewed /
   BasketItemAdded); `userId` login'de dolu, anonimde null ama `anonymousId` dolu. PII yok.
4. **Liste sayfası kaydı YOK** (049): ana sayfa/ürün listesi gezme → hiç sinyal yazılmaz.
   `ListShown`/`CategoryViewed` gövdesiyle manuel `POST /v1/signals` → öğe reddedilir (atlanır).

## Senaryo 3 — İzolasyon (US3, P3)

1. `personalization-api`'yi tamamen durdur.
2. Uçtan uca alışveriş: gezin → sepete ekle → sipariş tamamla.
3. Beklenen: **hatasız** tamamlanır; sayfalar normal hız. Gezinme sinyalleri o pencerede
   düşer (kayıp-toleranslı); satın-alma sinyali servis dönünce yakalanır (durable).

## Birim testleri (İlke VI — domain, test-first)

`dotnet test tests/Personalization.Api.Tests/Personalization.Api.Tests.csproj`

- `PurchaseSignal.Create`: boş kalem reddi, `Quantity>0`, `UnitPrice≥0`, geçerli oluşum.
- `PurchaseSignalItem` / VO invariant'ları.
- `BehaviorSignal.Create`: bilinmeyen `eventType` reddi (liste tipleri dahil), boş
  `anonymousId` reddi, geçerli oluşum (ProductViewed/BasketItemAdded).

## Başarı ölçütleri eşlemesi

| SC | Doğrulama |
|---|---|
| SC-001 (satın-alma %100 + 0 mükerrer) | Senaryo 1.3–1.5 |
| SC-002 (sayfa gecikmesi yok) | Senaryo 2.2 (Enqueue O(1), arka plan gönderim) |
| SC-003 (servis kapalı → 0 hata) | Senaryo 3 |
| SC-004 (gezinme yazım oranı) | Senaryo 2.3 |
| SC-005 (PII yok) | Senaryo 1.3 + 2.3 kayıt denetimi |

## Kapsam dışı (bu faz)

RFM/segment, öneri, ML/model, serving/okuma endpoint'i, WebApp gösterim, Python'a dosya
export, demografi/onboarding, kategori/marka enrichment, kimlik birleştirme.