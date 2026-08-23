# Procurement — Domain Süreci

**BC ne yapar:** Dış tedarikçi feed'lerini çeker, ürünleri barkod-anahtarlı bir **havuzda** toplar,
eksiksiz ürünü Catalog'a (satış) ve Stock'a (stok) yayınlar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

   1. **Tedarikçi feed'i zamanlı çekilir.** Her tedarikçi kendi        `(FeedPullJob → PullSupplierFeed)`
   ucundan, kendi feed şekliyle okunur; bir tedarikçinin hatası
   diğerini kesmez.
2. **Yabancı feed şekli iç modele çevrilir** (Anti-Corruption).      `(ISupplierFeedAdapter)`
   Her tedarikçinin sözlüğü (ör. gtin/cost) tek nötr satıra iner.
3. **Her ürün BARKODUYLA havuza toplanır.** Barkod global tekil →   `(PoolProduct.UpsertListing)`
   bir barkod = tek tedarikçi. Satır koşulsuz güncellenir.
4. **Feed'den düşen ürün "listeden çıktı" işaretlenir.** Silinmez;  `(PoolProduct.MarkDelisted)`
   stok 0 olur ama vitrinde kalır.
5. **Kanonik içerik ürünün satırından kurulur.** Ad/kategori/ölçü   `(PoolProduct.RebuildCanonical)`
   tek kaynaktan. Eksik kalırsa Pending (yayınlanmaz).
6. **Eksiksiz ürün + güncel fiyat/stok KANONİK yayınlanır.**        `(PublishPoolProduct`
   Catalog satışa alır, Stock stoğu yazar. Tek kanal.                ` → CanonicalProductUpserted)`
7. **Değişmeyen ürün sessizdir** — içerik/fiyat/stok değişmediyse   `(PoolProduct.TryTakePublish)`
   tekrar yayın yok (idempotency tek noktada).

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik, global tekil.** İki tedarikçi aynı barkodu paylaşmaz (buy-box rekabeti bırakıldı, 047).
- **Feed = otorite.** Ürün yalnız feed'den doğar/güncellenir; elle CRUD yok. Feed'den düşen delist olur.
- **Ürün eksiksiz gelir.** Kanonik eksikse (ad/kategori/açıklama) Pending kalır, yayınlanmaz — AI tamamlama yok.
- **Dış dünyaya tek yol = event.** Catalog/Stock'a yalnız `CanonicalProductUpserted` + `ProductLinked` fanout.
- **Saga yok.** Dayanıklılık = idempotent upsert + retry + error queue (durable lokal kuyruk).

## Sınır (bu BC'nin dokunmadığı)

Fiyatlandırma/indirim, sipariş, ödeme yok. Tedarikçiye sipariş bildirimi = ayrı BC (048, planlı).
