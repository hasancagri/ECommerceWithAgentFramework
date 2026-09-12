# Order — Domain Süreci

**BC ne yapar:** Adresi + sepet kalemlerini bir **siparişe** bağlar; siparişi Pending doğurur, checkout
orkestratörünün komutlarıyla Confirmed/Cancelled'a alır ve satın-alma kanıtını yayar. **Çekim + chat
`place_order` 076'da SÖKÜLDÜ** (kart-saklama + charge yolu kaldırıldı; checkout geçici boşlukta, ödeme
hosted-CF'e taşınacak — [[hosted-cf-checkout-pivot]]). Stok/telafi/sepet temizliği saga'nın işi.

> Domain-önce anlatı. Sağdaki `(…)` = koda köprü. Süreç değişince güncellenir; guard rename'i yakalar.

## Süreç

1. **Checkout orkestratörü sipariş komutlarını gönderir.** Order      `(OrderEventHandlers →`
   `Create/Confirm/Cancel`'ı tüketir; aggregate davranışını çalıştırır  ` OrderCreated/OrderConfirmed)`
   sonucu reply kuyruğuna yayar. Sipariş Pending doğar (PaymentId=CheckoutId, idempotent).
2. **PIVOT: Confirm satın-alma kanıtını yayar.** Sipariş Confirmed    `(OrderEventHandlers →`
   olunca `OrderCompleted` fanout'u yayılır (Reviews/Storefront).       ` OrderCompleted)`
   Idempotent: Confirmed'den tekrar yayınlamaz.
3. **Kullanıcı siparişlerini okur.** Kişi kendi geçmişini listeler.   `(GetOrdersForAgent)`

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`.
- **Idempotency: `PaymentId`(=CheckoutId).** Aynı checkout → tek sipariş. Confirm/Cancel yalnız Pending'den.
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.
- **Çekim/place_order YOK (076).** Chat charge yolu (PaymentAttempt/reconcile/PG client) söküldü; sipariş
  tetiği hosted-CF ödeme-başarılı'dan gelecek (ayrı spec).

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (hosted-CF → PG), stok commit döngüsü + telafi + sepet temizliği + watchdog **Checkout.
Orchestrator** sağasının. Order stok yazmaz, fiyat/indirim hesaplamaz, ürün bilmez.
