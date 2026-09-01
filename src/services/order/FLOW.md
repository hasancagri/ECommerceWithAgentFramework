# Order — Domain Süreci

**BC ne yapar:** Çekilmiş ödemeyi + adresi + sepet kalemlerini bir **siparişe** bağlar; siparişi Pending
doğurur, dış orkestratörün komutlarıyla Confirmed/Cancelled'a alır ve satın-alma kanıtını yayar. Stok
commit döngüsü + telafi + sepet temizliği **bu BC'de değil** — Checkout.Orchestrator sağasının işidir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Chat yolu: ödeme önce çekilir, sipariş sonra.** Sepet snapshot'ı  `(PlaceOrderCommandHandler`
   (gRPC otorite) + kayıtlı kart/adres alınır, PG'den idempotent          ` → PaymentGatewayClient)`
   çekim yapılır. LLM yalnız `place_order` seçer; para/güven sunucuda.
2. **Çekim sonucu karara döner.** Başarı + tutar sepetle uyuşur →         `(PaymentAttempt.OnChargeResult)`
   sipariş; kesin hata → red; belirsiz → durable reconcile'a devir.
3. **Sipariş Pending doğar, StartCheckout orkestratöre yayılır.**        `(PaymentOrderCreator`
   `PaymentId` = correlation-key'ten türeyen deterministik Guid            ` → StartCheckout)`
   (çift sipariş yok). Sonraki commit/confirm adımlarını sağa sürer.
4. **Web/orkestratör yolu: Order sipariş komutlarını tüketir.**          `(OrderEventHandlers →`
   Checkout.Orchestrator `Create/Confirm/Cancel` gönderir; Order           ` OrderCreated/OrderConfirmed)`
   aggregate davranışını çalıştırır, sonucu reply kuyruğuna yayar.
5. **PIVOT: Confirm satın-alma kanıtını yayar.** Sipariş Confirmed       `(OrderEventHandlers →`
   olunca `OrderCompleted` fanout'u yayılır; Reviews tüketir (gRPC yok).   ` OrderCompleted)`
   Idempotent: Confirmed'den tekrar yayınlamaz.
6. **Belirsiz çekim sınırlı reconcile edilir.** PG retrieve →            `(PaymentReconcileHandler`
   gecikmeli başarı siparişi kurar; deadline dolarsa terminal              ` → OnReconcileTick ← ReconcileTick)`
   (NeedsReconciliation, ops görünürlük). Asla çift çekim/sonsuz.

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`; ileri gitmiş sipariş değişmez.
- **Idempotency iki katman.** Çekim = correlation-key; sipariş = `PaymentId` (aynı sepet+taksit → tek sipariş). Confirm/Cancel yalnız Pending'den.
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; komut idempotent olduğundan tekrar yayınlanmaz.
- **Para/güven asla LLM'de.** Agent slice yalnız `PlaceOrderCommand` seçer; sepet/adres/çekim sunucu-otoritesi.

## Sınır (bu BC'nin dokunmadığı)

Stok commit döngüsü + LIFO telafi (revert) + watchdog timeout + sepet temizliği + commit/charge sıralaması
**Checkout.Orchestrator** sağasının (`CheckoutProcess`); Order o adımları bilmez, yalnız kendi aggregate
komutlarına yanıt verir. Stok düşümü Stock BC'nin; sepet içeriği Basket BC'nin; kart vault + gerçek çekim
PaymentGateway'in. Order stok yazmaz, fiyat/indirim hesaplamaz, ürün bilmez.