# Catalog — Domain Süreci

**BC ne yapar:** Satılabilir zengin ürünü (ad/fiyat/yazar/yayınevi/kategori/ölçü/SEO/özellik) tutar, vitrine açar
ve değişimi Storefront'a bildirir. Ürünler **first-party**: mağaza sahibi ekler/günceller (feed yok).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Çok-tedarikçi feed (Procurement) söküldü; model first-party'ye geçti. Ürün **yazım
> yolu (ürün-CRUD)** sonraki feature'da gelir. Aşağıki adım 1 o girişi bekler; adım 2+ domain davranışları
> (aggregate metotları) ürün oluşturulunca aynen işler.

> **051 notu (kitap import):** İlk gerçek yazıcı = açılış kitap import'u (`books.json` → her kitap
> `ImportBook` command). ProductId ISBN'den deterministik türer (idempotent upsert). Yayın kapısı
> **fiyat>0** (fiyatsız kitap taslak kalır, event yayılmaz). Omurga (`ProductAdded`) bu feature'da uyandı.

## Süreç

1. **Ürün komutla oluşturulur/güncellenir** (051 kitap import veya       `(ImportBook, UpdateProduct,`
   058 admin düzenleme; ISBN/ad/fiyat girdi). Deterministik id ile      ` Product.Create, Product.Rename)`
   bulun-veya-kur (idempotent upsert).
1a. **Fiyat her gerçek değişimde geçmişe yazılır (058).** İlk fiyat      `(Product.SetPrice,`
   ilk satırdır; aynı fiyatla kayıt satır düşürmez (append-only).       ` ProductPriceChange)`
2. **Yazar(lar) + yayınevi bulun-veya-doğur.** Her yazar adı           `(Author.Create, Product.SetAuthors,`
   normalize+aranır (çok-çok, Id listesi); yayınevi tek (çok-bir).      ` Publisher.Create, Product.SetPublisher)`
   Yoksa girdiden doğar, Id ile referanslanır.
3. **Kategori seed'li ağaçtan çözülür.** Primary atama = seçilen        `(NameNormalization.Normalize,`
   kategori; bayat atamalar düşürülür.                                  ` Product.AssignToCategory, Product.RemoveFromCategory)`
4. **Kimlik + aile yazılır.** SKU/GTIN girdiden, aile kodu opsiyonel    `(Product.SetIdentifiers,`
   (null = ailesiz).                                                    ` Product.SetFamilyCode)`
5. **Ölçü + SEO doldurulur.** Ölçü girdiden (0 = bilinmiyor), SEO       `(Product.SetDimensions,`
   ad/açıklamadan türetilir.                                            ` Product.SetSeo)`
6. **Özellikler registry'den Id'ye çözülüp TAM yazılır.**              `(Product.SetSpecifications)`
   Bilinmeyen ad opsiyoneldir — yok sayılır, satır spec'siz ilerler.
7. **Yayın anahtarı admin'dedir (058) — kapı fiyat>0.** Fiyatsız         `(SetProductPublished,`
   yayına alma reddedilir; yayından kaldırma vitrini gizler (silmez).   ` Product.Publish, Product.Unpublish)`
   Düzenleme yayın durumunu DEĞİŞTİRMEZ (koruma).
8. **Değişim Storefront'a KANONİK yayınlanır.** Fiyat decimal,          `(ProductChangedEvent)`
   kategori = primary; yazarlar (Id+ad çifti) + yayınevi + özellikler
   ADLA taşınır (fat event; tüketici lookup yapmaz). Yalnız YAYINDAKİ
   ürün yayar; yayından kaldırma `IsDeleted:true` ile gizletir (058).
9. **Yalnız YAYINLANAN üründe Stock'a bağ kurulur.** Barkod→ürün         `(ProductAdded)`
   eşlemesi + ilk OnHand yazılır (taslak = event yok).

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik.** Ürün GTIN'iyle bulunur; aynı barkod tek ürüne düşer.
- **Kategori zorunlu, spec opsiyonel.** Kategori çözülemezse reddedilir; bilinmeyen spec sessiz düşer.
- **Yayın kapısı = fiyat>0 (051).** Fiyatsız ürün yayınlanamaz (satılamaz kart); taslak kalır, event yayılmaz.
- **Fiyat geçmişi append-only (058).** Her gerçek fiyat değişimi (ve ilk fiyat) fiyatla aynı transaction'da satıra döner; satır silinmez/değişmez `(ProductPriceChange)`.
- **Silme yok (016 sürer).** Ürün silinmez; `IsDeleted:true` yalnız yayından-kaldırmanın vitrin-gizleme bayrağıdır (058) — kayıt Catalog'da yaşamaya devam eder.
- **Dış dünyaya iki yol = event.** Storefront'a `ProductChangedEvent` (her değişim), Stock'a `ProductAdded` (yayınlanan).

## Sınır (bu BC'nin dokunmadığı)

Stok miktarı (OnHand) Stock BC'de; vitrin okuma-modeli + facet/arama Storefront'ta. İndirim/fiyatlandırma-
motoru, sipariş, ödeme yok.