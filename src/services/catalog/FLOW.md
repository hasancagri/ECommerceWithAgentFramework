# Catalog — Domain Süreci

**BC ne yapar:** Procurement'ın yayınladığı **kanonik ürünü** tüketir, satılabilir zengin ürünü
(ad/fiyat/marka/kategori/ölçü/SEO/özellik) kurar/günceller, vitrine açar ve değişimi Storefront'a bildirir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kanonik ürün olayı tüketilir.** Barkod-anahtarlı, sıralı işlenir   `(CatalogEventHandlers`
   (aynı barkodun olayları çakışmaz).                                     ` ← CanonicalProductUpserted)`
2. **Marka bulun-veya-doğur.** Ad normalize edilip aranır; yoksa        `(Brand.Create)`
   feed'den doğar. Markasız kanonik ürün yok sayılır (doğum feed'den).
3. **Kanonik alt kategori seed'li ağaçtan çözülür.** Çözülemezse        `(Category.Create,`
   bu BUG'dır → exception → retry → error queue (veri durumu değil).     ` NameNormalization.Normalize)`
4. **Ürün barkoduyla bulun-veya-kur.** Varsa ad/açıklama/fiyat          `(Product.Create,`
   güncellenir; yoksa yeni ürün doğar. Fiyat negatifse yok sayılır.      ` Product.Rename, Product.SetPrice)`
5. **Kimlik + aile + marka yazılır.** SKU/GTIN kanonikten, aile kodu    `(Product.SetIdentifiers,`
   feed'den (null = aileden çıkar), marka Id ile referanslanır.          ` Product.SetFamilyCode, Product.SetBrand)`
6. **Ölçü + SEO doldurulur.** Ölçü feed'den (0 = bilinmiyor), SEO        `(Product.SetDimensions,`
   kanonik ad/açıklamadan türetilir.                                     ` Product.SetSeo)`
7. **Primary kategori atanır, bayat atamalar düşürülür.** Feed'de       `(Product.AssignToCategory,`
   kategori değiştiyse eski atama silinir (tek primary = alt kategori).  ` Product.RemoveFromCategory)`
8. **Kanonik özellikler registry'den Id'ye çözülüp TAM yazılır.**       `(Product.SetSpecifications)`
   Bilinmeyen ad opsiyoneldir — yok sayılır, satır spec'siz ilerler.
9. **Ürün satışa/vitrine açılır.** Yazım yolu her kanonikte publish     `(Product.Publish)`
   eder — kanonik ürün vitrindedir.
10. **Değişim Storefront'a KANONİK yayınlanır.** Fiyat decimal,         `(ProductChangedEvent)`
    kategori = primary; özellikler ADLA taşınır (Id çıkmaz).
11. **Yalnız YENİ üründe Stock'a bağ kurulur.** Barkod→ürün eşlemesi    `(ProductLinked)`
    + ilk OnHand yazılır (yarış edge'i kapanır).

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik.** Ürün GTIN'iyle bulunur; aynı barkod tek ürüne düşer (Procurement barkodu tekiller).
- **Doğum yalnız feed'den.** Marka/kategori/ürün elle CRUD ile değil, kanonik olaydan get-or-create ile doğar.
- **Kategori zorunlu, spec opsiyonel.** Kategori çözülemezse exception (seed BUG'ı); bilinmeyen spec sessiz düşer.
- **Silme yok.** Ürün silinmez; `ProductChangedEvent` `IsDeleted` hep false yayınlanır (016 kararı sürer).
- **Dış dünyaya iki yol = event.** Storefront'a `ProductChangedEvent` (her değişim), Stock'a `ProductLinked` (yeni).

## Sınır (bu BC'nin dokunmadığı)

Stok miktarı (OnHand) Stock BC'de; feed çekme/merge/AI-zenginleştirme Procurement'ta; vitrin okuma-modeli +
facet/arama Storefront'ta. İndirim/fiyatlandırma-motoru, sipariş, ödeme yok.
