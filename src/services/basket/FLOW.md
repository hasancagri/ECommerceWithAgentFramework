# Basket — Domain Süreci

**BC ne yapar:** Kullanıcının kalıcı sepetini + kalemlerini tutar. Sahip login kullanıcı YA DA
anonim ziyaretçi olabilir (057); ikisi aynı modeldir, fark yalnız kimliğin kaynağı. Sepet stok
TUTMAZ ve süre İŞLETMEZ (056); stok gerçeği checkout anındadır.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kullanıcı ürünü sepete atar.** Sepet yoksa oluşur; adet 1 artar,    `(AddBasketItemCommandHandler`
   satır upsert edilir. Stok'a hiçbir çağrı gitmez.                       ` → Basket.SetItem)`
2. **Adet mutlak değere getirilir.** `≤0` ise satır çıkar; 5 tavanı      `(SetBasketItemQuantityCommandHandler)`
   aşılamaz.
3. **Satır elle silinir.** Yalnız sepet belgesi değişir.                  `(DeleteBasketItemCommandHandler`
                                                                          ` → Basket.RemoveItem)`
4. **Login'de anonim sepet hesaba taşınır.** Kalemler tek sepette        `(MergeBasketCommandHandler`
   toplanır; ortak üründe adetler toplanıp tavana sabitlenir; anonim      ` → Basket.MergeFrom)`
   sepet silinir (ikinci merge sessiz no-op).
5. **Checkout sepeti boşaltır (hand-off).** Orchestrator pivot-sonrası   `(BasketEventHandlers`
   broker komutuyla çağırır; sepet silinir (idempotent).                  ` → ClearBasketByCheckoutCommandHandler)`

## Domain kuralları (süreci yöneten değişmezler)

- **Sepet kalıcıdır (056).** Zamana bağlı hiçbir üye/temizlik yok; terk edilmiş sepet süresiz durur.
- **Anonim sahiplik meşrudur (057).** Ekleme/okuma/adet/silme kimlik doğrulama İSTEMEZ; sahip tahmin
  edilemez Guid'dir (token `sub` ya da anonim kimlik). Yalnız merge login ister; yön tek: anonim → hesap.
- **Sepet stok tutmaz (056).** Ekleme/adet/silme Stock'a gitmez; yetersizlik checkout'ta `CommitStock` reddeder, saga telafi eder.
- **Sabit üst sınır otoriter.** Satır adedi `Basket.MaxItemQuantity` (5) üstüne çıkamaz — UI/API/agent farketmez.
- **Fiyat snapshot'tır.** Satır fiyatı ekleme anındaki vitrin fiyatıdır; sepette güncellenmez.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat kaynağı (Catalog/Storefront), OnHand stok otoritesi (Stock),
sipariş/ödeme (Checkout saga) yok. Checkout temizliği saga'nın adımı — Basket başlatmaz.