# Checkout.Orchestrator — Domain Süreci

**BC ne yapar:** Ödeme-akışını **orkestre eder** — siparişi oluşturur, stoğu kalem kalem commit eder,
ödemeyi çeker, siparişi onaylar, sepeti temizler; herhangi bir adım kesin başarısız olursa (pivot öncesi)
telafi eder. Broker-only sağa (kendi DB'si yok yerine state Marten belgesi); hiçbir BC'nin verisine dokunmaz.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Giriş: checkout başlar, senkron bekleme yok.** WebApp POST →       `(CheckoutEndpointExtension`
   `StartCheckout` yayılır; aynı kullanıcı+sepet → aynı CheckoutId        ` → StartCheckout)`
   (idempotent). Chat aynı `StartCheckout`'u AlreadyCaptured ile yayar.
2. **Sağa doğar + watchdog kurulur.** Web (Charge) → sipariş           `(CheckoutProcess`
   oluşturma ilk adım; chat (AlreadyCaptured) → sipariş+ödeme zaten       ` → CheckoutTimedOut)`
   var, doğrudan stok commit'e geçer.
3. **Sipariş oluştu → kalemler tek tek commit edilir (döngü).**        `(OrderCreated`
   Her başarı sonraki kalemi tetikler; oluşmadıysa telafi yok, biter.     ` → CommitStockCommand)`
4. **Tüm kalemler commit olunca ödeme çekilir (Charge=pivot) /**       `(StockCommitted →`
   **doğrudan onay (AlreadyCaptured, pivot geçildi).**                    ` ChargePaymentCommand)`
5. **PİVOT: ödeme tek-faz çekilir → sipariş onaylanır.** Başarısız      `(PaymentCharged`
   → para hareket etmedi, telafiye geç.                                   ` → ConfirmOrderCommand)`
6. **Sipariş onaylandı → sepet temizlenir.** Onay kalıcı başarısız      `(OrderConfirmed`
   ama ödeme çekildi → iptal ETME, logla + bitir (manuel müdahale).       ` → ClearBasketCommand)`
7. **Sepet temizliği süreci bitirir.** Başarı da hata da tamamlar;      `(BasketCleared`
   sepet temizlenemese bile sipariş Confirmed KALIR (FR-018).            ` → MarkCompleted)`
8. **Telafi (yalnız pivot öncesi): stok LIFO geri sarılır → iptal.**   `(StockCommitReverted →`
   Commit edilmiş her kalem tersten revert; kalmayınca sipariş iptal.     ` RevertCommitStockCommand/CancelOrderCommand)`
9. **Watchdog güvenceye alır.** Pivot ÖNCESİ (CreatingOrder/           `(CheckoutTimedOut`
   CommittingStock) takılırsa telafi; Charging + SONRASI iptal ETMEZ      ` → CheckoutPhases)`
   (belirsizlik: para çekilmiş olabilir), sadece bitirir.

## Domain kuralları (süreci yöneten değişmezler)

- **Pivot = Charge (tek-faz tahsilat, SON adım).** Öncesi geri-alınabilir (stok revert + sipariş cancel, para hareket etmez); sonrası geri-alma YOK — void/refund söküldü (kullanıcı kararı).
- **Broker-only, senkron çağrı YOK (İlke I).** Her adım RabbitMQ komut/yanıtı; state Marten belgesi (`Id`=CheckoutId), her adım atomik persist → restart'a dayanır (FR-020).
- **Geçici hata sağada değil.** Erişilemezlik → hedef BC fırlatır, Wolverine komutu yeniden dener; sağa yalnız KESİN sonucu görür (başarı → ilerle, kalıcı hata → telafi).
- **İki ödeme kaynağı (FR-030).** `PaymentMode.Charge` = mock tek-faz (web); `AlreadyCaptured` = dış PG çekti (chat) → charge atlanır, sipariş zaten oluşmuştur.
- **Bayat-mesaj guard.** `Phase` etiketi telafi başlayınca geç gelen commit yanıtını no-op'lar; tamamlanmış sağaya geç watchdog sessizce düşer (FR-026).

## Sınır (bu BC'nin dokunmadığı)

Sipariş durumu Order BC'nin; stok commit/revert Stock BC'nin; gerçek çekim Payment + PaymentGateway'in;
sepet içeriği Basket BC'nin. Orchestrator hiçbirinin DB'sine/aggregate'ine dokunmaz — yalnız komut yayar,
yanıt bekler, sonraki adımı sürer. Ürün/fiyat bilmez, para hesaplamaz (tutar girişte gelir).