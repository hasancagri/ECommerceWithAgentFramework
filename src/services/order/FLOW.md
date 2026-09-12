# Order — Domain Süreci

**BC ne yapar:** Adresi + sepet kalemlerini bir **siparişe** bağlar; siparişi Pending doğurur, dış
orkestratörün komutlarıyla Confirmed/Cancelled'a alır ve satın-alma kanıtını yayar. **Çekim bu BC'de
DEĞİL** (075: Payment BC'nin işi, PG NON-3D); stok commit döngüsü + telafi + sepet temizliği de
Checkout.Orchestrator sağasının işidir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Chat yolu: onay alınır, checkout tetiklenir (çekim YOK).** Sepet    `(PlaceOrderCommandHandler`
   snapshot'ı (gRPC otorite) + kayıtlı kart/adres ön-kontrolü; kullanıcı   ` → StartCheckout)`
   `confirmed:true` vermezse çekim başlamaz (FR-014). LLM yalnız
   `place_order` seçer; tutar/adres/kalem sunucuda. Deterministik
   CheckoutId (userId+sepet) → çift sipariş yok.
2. **Web/orkestratör yolu: Order sipariş komutlarını tüketir.**          `(OrderEventHandlers →`
   Checkout.Orchestrator `Create/Confirm/Cancel` gönderir; Order           ` OrderCreated/OrderConfirmed)`
   aggregate davranışını çalıştırır, sonucu reply kuyruğuna yayar.
   Sipariş Pending doğar; `PaymentId` = CheckoutId (idempotent).
3. **PIVOT: Confirm satın-alma kanıtını yayar.** Sipariş Confirmed       `(OrderEventHandlers →`
   olunca `OrderCompleted` fanout'u yayılır; Reviews/Storefront tüketir.   ` OrderCompleted)`
   Idempotent: Confirmed'den tekrar yayınlamaz.

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`; ileri gitmiş sipariş değişmez.
- **Idempotency: `PaymentId` (=CheckoutId).** Aynı sepet → deterministik CheckoutId → tek sipariş. Confirm/Cancel yalnız Pending'den.
- **Onaysız çekim yok (FR-014).** `place_order` `confirmed:true` şart; NON-3D'de banka ekranı yerine agent onayı.
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; komut idempotent → tekrar yayınlanmaz.
- **Para/güven asla LLM'de.** Agent slice yalnız `PlaceOrderCommand` seçer; sepet/adres sunucu-otoritesi, çekim Payment BC.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (NON-3D, PG) **Payment BC**'nin (075: Order artık çekmez); stok commit döngüsü + LIFO telafi +
watchdog + sepet temizliği + commit/charge sıralaması **Checkout.Orchestrator** sağasının (`CheckoutProcess`).
Order o adımları bilmez, yalnız kendi aggregate komutlarına yanıt verir. Stok düşümü Stock BC'nin; sepet
içeriği Basket BC'nin; kart verisi + çekim PG/Payment'ın. Order stok yazmaz, fiyat/indirim hesaplamaz.
