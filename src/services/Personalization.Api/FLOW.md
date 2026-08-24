# Personalization.Api — Domain Süreci

**BC ne yapar:** Kişiselleştirme için ham sinyalleri kendi deposunda biriktirir (write-only). İki
kaynak: kullanıcı gezinmesi (telemetri) ve tamamlanan satın-almalar. Bu faz yalnız TOPLAR; öneri /
segment / model / gösterim üretmez (sonraki fazlar).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

### A) Gezinme sinyali (kayıp-toleranslı)

1. **Kullanıcı siteyi gezer** (ürün görüntüler, liste görür, sepete ekler);   `(WebApp`
   WebApp sinyali arka plan kuyruğuna bırakır — sayfa hiç beklemez.           ` BehaviorLogWriter.Enqueue)`
2. **WebApp sinyalleri batch olarak gönderir.** Arka plan işçi kuyruktan     `(BehaviorLogWriter →`
   toplar, m2m token'la POST eder; erişilemezse sessizce düşer.              ` IPersonalizationRefitService.PostSignals)`
3. **BC her sinyali doğrular + yazar.** Bilinen tip + dolu anonim/oturum     `(IngestBehaviorSignals`
   kimliği; geçersiz öge atlanır, geçerliler saklanır.                       ` → BehaviorSignal.Create)`

### B) Satın-alma sinyali (kayıpsız)

4. **Sipariş ödeme onaylı tamamlanır** (Order CheckoutSaga başarı/pivot);    `(IntegrationEvents`
   Order `OrderCompleted` event yayar (yalnız gerçek/ödenmiş satın-alma).    ` .OrderCompleted)`
5. **BC event'i dinler + satın-alma sinyalini yazar.** Idempotent           `(PersonalizationEventHandlers`
   (Id=OrderId; yeniden teslimde no-op); kalemler VO ile kurulur.           ` .Handle)`
6. **Satın-alma aggregate'i invariant'ları korur.** En az 1 kalem,          `(PurchaseSignal.Create`
   adet>0, tutar≥0; kalem kategori/marka null olabilir (Order tutmuyorsa).  ` → PurchaseSignalItem.Create)`

## Domain kuralları (süreci yöneten değişmezler)

- **İki sinyal, iki dayanıklılık.** Gezinme = kayıp-toleranslı (client kuyruğu + DropWrite + drop-on-
  fail); satın-alma = kayıpsız (durable event + retry + idempotent). Karıştırılmaz.
- **Satın-alma yalnız ödeme onaylı tamamlanınca.** Oluşturulan/ödenmemiş/iptal sipariş sinyal üretmez
  (tetik = CheckoutSaga pivot).
- **Idempotent satın-alma.** `PurchaseSignal.Id = OrderId`; aynı `OrderCompleted` yeniden teslimde
  mükerrer kayıt oluşmaz.
- **PII yok.** Yalnız opak kimlikler (user/anonim/oturum) + davranış/işlem alanları saklanır.
- **BehaviorSignal telemetridir, aggregate değil** (write-once; anemik-aggregate açılmaz — İLKE II).
- **Yazım kaynağı ikisiyle sınırlı:** WebApp (BFF) HTTP + Order event. Başka BC doğrudan yazmaz.

## Sınır (bu BC'nin dokunmadığı)

Öneri/segment (RFM)/model eğitimi/serving/okuma yüzeyi YOK — sinyal yalnız girer ve oturur. Kategori/
marka zenginleştirmesi (ProductId→katalog) yok. Kimlik birleştirme (anonim→kullanıcı) yok. Model
tüketimi ileride ayrı süreçte ele alınır.