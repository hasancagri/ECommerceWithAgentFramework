# Order — Domain Süreci

**BC ne yapar:** Sepeti + adresi + ödemeyi bir **siparişe** bağlar. Ödeme çekilince siparişi Pending
doğurur ve dayanıklı **CheckoutSaga** ile stoğu commit eder, sepeti temizler, siparişi Confirmed'e alır;
herhangi bir adım kırılırsa commit edilmiş stoğu geri alıp siparişi iptal eder.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Ödeme önce çekilir, sipariş sonra doğar.** Sepet             `(PlaceOrderCommandHandler`
   snapshot'ı (gRPC otorite) + kayıtlı kart/adres alınır, PG'den     ` → PaymentGatewayClient)`
   idempotent çekim yapılır. Web akışında da çekim sipariş öncesidir.
2. **Çekim sonucu karara döner.** Başarı + tutar sepetle uyuşur →   `(PaymentAttempt.OnChargeResult)`
   sipariş; kesin hata → red; belirsiz → durable reconcile'a devir.
3. **Sipariş Pending doğar, StartCheckout atomik yayınlanır.**      `(PaymentOrderCreator`
   `PaymentId` = correlation-key'ten türeyen deterministik Guid       ` → StartCheckout)`
   (çift sipariş yok); saga kaydı Marten commit'iyle atomik.
4. **Saga her kalemin stoğunu tek tek commit eder.** Başarı →       `(CheckoutSaga.OnCommitResult`
   sonraki kalem; teknik erişilemezlik → sınırlı retry; iş hatası     ` → StockCommitClientProxy.CommitAsync)`
   → telafiye geçilir (oversell yasak, fail-closed).
5. **PIVOT: tüm kalemler commit olunca sipariş Confirmed yazılır**  `(CheckoutSaga.Confirm →`
   **— sepet temizliğinden ÖNCE.** Bu noktadan sonra iptal YASAK.     ` BasketClearClientProxy.ClearAsync)`
   Sepet temizliği başarısız olursa retry + log, sipariş Confirmed kalır.
6. **Kalem commit'i kırılırsa telafi başlar.** Commit edilmiş her   `(CheckoutSaga.OnRevertResult`
   kalem TEK TEK geri alınır; kalem kalmayınca sipariş iptal +        ` → RevertCommitAsync)`
   saga biter. Revert kalıcı başarısız → CompensationFailed alarmı.
7. **Watchdog süreci güvenceye alır.** Pivot ÖNCESİ takılırsa       `(CheckoutSaga.OnTimeout`
   telafi + iptal; pivot SONRASI iptal etmez, sadece bitirir.         ` ← CheckoutTimedOut)`
8. **Belirsiz çekim sınırlı reconcile edilir.** PG retrieve →       `(PaymentReconcileHandler`
   gecikmeli başarı siparişi kurar; deadline dolarsa terminal          ` → OnReconcileTick ← ReconcileTick)`
   (NeedsReconciliation, ops görünürlük). Asla çift çekim/sonsuz.
9. **Satın-alma kanıtı olayla yayılır.** Confirmed sipariş →        `(OrderEventHandlers`
   `OrderCompleted` (fanout); Reviews/Personalization tüketir (gRPC yok). ` → OrderCompleted)`

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`; ileri gitmiş sipariş değişmez.
- **Pivot kuralı (FR-009).** Confirm sepet temizliğinden önce yazılır; pivot sonrası hiçbir başarısızlık siparişi iptal etmez.
- **Idempotency iki katman.** Çekim = correlation-key; sipariş = `PaymentId` (aynı sepet+taksit → tek sipariş).
- **Dayanıklılık = durable saga + reconcile.** Adımlar `StartCheckout`/`CommitNextItem` mesajlarıyla; her adım atomik persist, restart'a dayanır.
- **Para/güven asla LLM'de.** Agent slice yalnız `PlaceOrderCommand` seçer; sepet/adres/çekim sunucu-otoritesi.

## Sınır (bu BC'nin dokunmadığı)

Stok düşümü Stock BC'nin (yalnız gRPC commit/revert); sepet içeriği Basket BC'nin; kart vault + gerçek
çekim PaymentGateway'in. Order stok yazmaz, fiyat/indirim hesaplamaz, ürün bilmez.
