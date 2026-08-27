# Catalog — Domain Süreci

**BC ne yapar:** Satılabilir zengin ürünü (ad/fiyat/marka/kategori/ölçü/SEO/özellik) tutar, vitrine açar
ve değişimi Storefront'a bildirir. Ürünler **first-party**: mağaza sahibi ekler/günceller (feed yok).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Çok-tedarikçi feed (Procurement) söküldü; model first-party'ye geçti. Ürün **yazım
> yolu (ürün-CRUD)** sonraki feature'da gelir. Aşağıki adım 1 o girişi bekler; adım 2+ domain davranışları
> (aggregate metotları) ürün oluşturulunca aynen işler.

## Süreç

1. **Ürün admin komutuyla oluşturulur/güncellenir** (ürün-CRUD —        `(Product.Create,`
   gelecek feature; barkod/ad/fiyat girdi). Barkodla bulun-veya-kur.    ` Product.Rename, Product.SetPrice)`
2. **Marka bulun-veya-doğur.** Ad normalize edilip aranır; yoksa        `(Brand.Create,`
   girdiden doğar. Marka Id ile referanslanır.                          ` Product.SetBrand)`
3. **Kategori seed'li ağaçtan çözülür.** Primary atama = seçilen        `(NameNormalization.Normalize,`
   kategori; bayat atamalar düşürülür.                                  ` Product.AssignToCategory, Product.RemoveFromCategory)`
4. **Kimlik + aile yazılır.** SKU/GTIN girdiden, aile kodu opsiyonel    `(Product.SetIdentifiers,`
   (null = ailesiz).                                                    ` Product.SetFamilyCode)`
5. **Ölçü + SEO doldurulur.** Ölçü girdiden (0 = bilinmiyor), SEO       `(Product.SetDimensions,`
   ad/açıklamadan türetilir.                                            ` Product.SetSeo)`
6. **Özellikler registry'den Id'ye çözülüp TAM yazılır.**              `(Product.SetSpecifications)`
   Bilinmeyen ad opsiyoneldir — yok sayılır, satır spec'siz ilerler.
7. **Ürün satışa/vitrine açılır.**                                      `(Product.Publish)`
8. **Değişim Storefront'a KANONİK yayınlanır.** Fiyat decimal,          `(ProductChangedEvent)`
   kategori = primary; özellikler ADLA taşınır (Id çıkmaz).
9. **Yalnız YENİ üründe Stock'a bağ kurulur.** Barkod→ürün eşlemesi     `(ProductLinked)`
   + ilk OnHand yazılır.

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik.** Ürün GTIN'iyle bulunur; aynı barkod tek ürüne düşer.
- **Kategori zorunlu, spec opsiyonel.** Kategori çözülemezse reddedilir; bilinmeyen spec sessiz düşer.
- **Silme yok.** Ürün silinmez; `ProductChangedEvent` `IsDeleted` hep false yayınlanır (016 kararı sürer).
- **Dış dünyaya iki yol = event.** Storefront'a `ProductChangedEvent` (her değişim), Stock'a `ProductLinked` (yeni).

## Sınır (bu BC'nin dokunmadığı)

Stok miktarı (OnHand) Stock BC'de; vitrin okuma-modeli + facet/arama Storefront'ta. İndirim/fiyatlandırma-
motoru, sipariş, ödeme yok.