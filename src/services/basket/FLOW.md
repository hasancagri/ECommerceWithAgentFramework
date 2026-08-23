# Basket — Domain Süreci

**BC ne yapar:** Kullanıcının sepetini + kalemlerini tutar; her adet değişiminde stoğu Stock'a
**senkron gRPC** ile rezerve eder (oversell yok, fail-closed), TTL çapası dolunca satırları düşürür.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kullanıcı ürünü sepete atar.** Sepet yoksa oluşur; süresi dolmuş     `(AddBasketItemCommandHandler`
   sepet önce tembel temizlenir (satırlar düşer, çapa sıfırlanır).        ` → PurgeExpiredItems)`
2. **Yeni toplam adet Stock'ta rezerve edilir (ayna).** Adet 1 artar;    `(StockReservationClientProxy`
   Stock erişilemez/yetersizse sepete YAZILMAZ — fail-closed.             ` .SetReservedQuantityAsync)`
3. **Rezervasyon başarılıysa satır sepete işlenir (upsert).** Son        `(Basket.SetItem)`
   bilinen kalan serbest stok satırda saklanır (efektif max hesabı).
4. **İlk başarılı eklemede sepet çapası kurulur.** Mutlak bitiş anı      `(Basket.StartReservation)`
   (now + Duration); sonraki ekleme/adet/silme çapaya DOKUNMAZ.
5. **Adet mutlak değere getirilir.** Yeni adet Stock'ta aynalanır;       `(SetBasketItemQuantityCommandHandler)`
   `≤0` ise satır çıkar + rezervasyon bırakılır (best-effort Release).
6. **Satır elle silinir.** Sepetten çıkınca rezervasyon ANINDA bırakılır  `(DeleteBasketItemCommandHandler`
   (Available yükselir); son satır giderse çapa sıfırlanır.               ` → Basket.RemoveItem)`
7. **TTL dolan rezervasyon satırı temizlenir.** Stock süre bitince       `(ReservationExpired`
   `ReservationExpired` yayınlar; ilgili satır sepetten düşer.            ` → BasketEventHandlers)`
8. **Checkout sepeti boşaltır (hand-off).** Order `CheckoutSaga`         `(BasketClearGrpcService`
   pivot-sonrası gRPC ile çağırır; sepet silinir (idempotent).           ` → ClearBasketByCheckoutCommandHandler)`

## Domain kuralları (süreci yöneten değişmezler)

- **Oversell yasak = fail-closed (FR-018).** Stock erişilemez/deadline aşarsa rezervasyon reddi → sepete yazma yok.
- **Sepet = rezervasyonun aynası.** Sepet adedi ≡ Stock'taki rezerve adet; her adet değişimi Stock'ta karara bağlanır.
- **Sabit üst sınır otoriter.** Satır adedi `Basket.MaxItemQuantity` (5) üstüne çıkamaz — UI/API/agent farketmez.
- **Tek mutlak çapa (017).** Bitiş sepet düzeyinde `ReservationExpiresAt`; ilk ekleme kurar, sepet boşalınca sıfırlanır.
- **Stok yazma otoritesi Basket'ta değil.** Basket yalnız rezerve eder (`SetReservedQuantity`/`Release`); OnHand'e dokunmaz.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat kaynağı (Catalog/Storefront), OnHand stok otoritesi (Stock),
sipariş/ödeme (Order saga) yok. Checkout temizliği saga'nın gRPC adımı — Basket başlatmaz.
