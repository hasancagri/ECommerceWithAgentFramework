# Catalog — Domain Süreci

**BC ne yapar:** Satılabilir zengin ürünü (ad/fiyat/yazar/yayınevi/kategori/ölçü/SEO/özellik) tutar, vitrine açar
ve değişimi Storefront'a bildirir. Ürünler **first-party**: mağaza sahibi ekler/günceller (feed yok).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Çok-tedarikçi feed (Procurement) söküldü; model first-party'ye geçti. Ürün **yazım
> yolu (ürün-CRUD)** sonraki feature'da gelir. Aşağıki adım 1 o girişi bekler; adım 2+ domain davranışları
> (aggregate metotları) ürün oluşturulunca aynen işler.

> **051 notu (kitap import):** İlk gerçek yazıcı = açılış kitap import'u (`books.json` → her kitap
> `ImportBook` command). ISBN `Product.Gtin`'de yaşar (idempotent upsert Gtin-query ile). Yayın kapısı
> **fiyat>0** (fiyatsız kitap taslak kalır, event yayılmaz). Omurga (`ProductAdded`) bu feature'da uyandı.

> **074 notu (MCP-only + elle giriş):** Domain iş REST yüzeyi söküldü — catalog admin/okuma tümüyle
> MCP (`/mcp` keşif + `/mcp-admin` yönetim). Doktrin kayması: import-only değil — admin `create_product`
> (`AdminCreateProduct`) ile elle künye girilir (TASLAK doğar; ISBN=Gtin çakışması reddedilir;
> yayın ayrı `admin_set_published`). Düzenleme = `AdminUpdateProduct` (058 ekran ikizi). `AdminCreateProduct`
> `ProductAdded`'i taslak anında yayınlar (InitialStock=0) — Stock bağı yayına kadar beklemez.

## Süreç

1. **Ürün komutla oluşturulur/güncellenir** (051 import, admin           `(ImportBook, AdminCreateProduct,`
   `create_product` elle giriş, veya `update_product` düzenleme;         ` AdminUpdateProduct, Product.Create, Product.Rename)`
   ISBN/ad/fiyat girdi). ISBN=Gtin ile bulun-veya-kur (idempotent).
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
7. **Yayın anahtarı admin'dedir (058) — kapı fiyat>0.** Fiyatsız         `(AdminSetPublished,`
   yayına alma reddedilir; yayından kaldırma vitrini gizler (silmez).   ` Product.Publish, Product.Unpublish)`
   Düzenleme yayın durumunu DEĞİŞTİRMEZ (koruma).
8. **Değişim Storefront'a KANONİK yayınlanır.** Fiyat decimal,          `(ProductChangedEvent)`
   kategori = primary; yazarlar (Id+ad çifti) + yayınevi + özellikler
   ADLA taşınır (fat event; tüketici lookup yapmaz). Yalnız YAYINDAKİ
   ürün yayar; yayından kaldırma `IsDeleted:true` ile gizletir (058).
9. **Stock'a bağ ürün DOĞARKEN kurulur.** Barkod→ürün eşlemesi + ilk      `(ProductAdded)`
   OnHand yazılır — import'ta yayınlanan kitapla aynı anda (InitialStock=100), elle
   `create_product` girişinde taslak doğar anda (InitialStock=0; admin sonra
   `admin_set_stock`/`admin_adjust_stock` ile gerçek adedi girer). BUGFIX: elle giriş
   önceden bu event'i hiç yayınlamıyordu — taslak yayına alınsa bile Stock'ta satır
   yoktu, checkout CommitStock RECORD_NOT_FOUND ile kalıcı reddediyordu.

10. **Keşif envanteri agent'a sunulur.** Yayındaki ürünlerde fiilen     `(ListCategories,`
   geçen kategori (üst-kategori ağacıyla), yazar ve yayınevi            ` ListAuthors,`
   listeleri; yayında olmayanın verisi sızmaz.                          ` ListPublishers)`

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik.** Ürün GTIN'iyle bulunur; aynı barkod tek ürüne düşer.
- **Kategori zorunlu, spec opsiyonel.** Kategori çözülemezse reddedilir; bilinmeyen spec sessiz düşer.
- **Yayın kapısı = fiyat>0 (051).** Fiyatsız ürün yayınlanamaz (satılamaz kart); taslak kalır, event yayılmaz.
- **Fiyat geçmişi append-only (058).** Her gerçek fiyat değişimi (ve ilk fiyat) fiyatla aynı transaction'da satıra döner; satır silinmez/değişmez `(ProductPriceChange)`.
- **Silme yok (016 sürer).** Ürün silinmez; `IsDeleted:true` yalnız yayından-kaldırmanın vitrin-gizleme bayrağıdır (058) — kayıt Catalog'da yaşamaya devam eder.
- **Dış dünyaya iki yol = event.** Storefront'a `ProductChangedEvent` (her değişim, yalnız yayındaki), Stock'a `ProductAdded` (ürün doğduğunda, taslak dahil — tek-seferlik, ISBN çakışma guard'ı tekrar tetiklenmeyi engeller).

## Sınır (bu BC'nin dokunmadığı)

Stok miktarı (OnHand) Stock BC'de; vitrin okuma-modeli + facet/arama Storefront'ta. İndirim/fiyatlandırma-
motoru, sipariş, ödeme yok.