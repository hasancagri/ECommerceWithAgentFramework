# File.Api — Domain Süreci (FLOW)

Mağazanın dosya (kapak görseli) **kayıt defteri**: bir mantıksal dosya (ImageName = ISBN) hangi fiziki
depolarda (R2/Local/…) yaşıyor izler; URL'i dış-depoya gitmeden, provider-agnostik çözer.

## Süreç

1. **Kaydet** — dosya yazılır: fiziki bitler `IFileStore` backend'ine (R2) yazılır, sonra kayıt defterine
   düşer. Kayıt yoksa yeni girdi, varsa o deponun konumu upsert edilir
   (`FileAsset.Create` / `FileAsset.AddOrReplaceLocation`).
2. **Konum ekle** — aynı dosya ikinci bir depoya (mirror) yazılırsa o depo tipi için konum eklenir; aynı
   depo tekrar yazılırsa yalnız path güncellenir, ikinci satır oluşmaz (`FileAsset.AddOrReplaceLocation`).
3. **Çöz** — tüketici ImageName'lerle URL ister; kayıt defterinden tercih edilen konum seçilip
   (`FileAsset.PreferredLocation`) config-base ile URL üretilir (`CoverUrlResolver.Resolve`) — dış-depoya
   çağrı yok, batch tek sorgu. Kayıtsız ImageName → boş (hata değil).
4. **Sil (konum)** — bir depodan çekilirse o konum çıkarılır; son konumsa reddedilir
   (`FileAsset.RemoveLocation` — konumsuz dosya olmaz).
5. **Kapak köprüsü (083)** — Catalog `ProductAdded` (yeni ürün, ISBN) tüketilir (`CatalogConsumers`):
   R2'de kapak varsa URL doğrudan çözülür; yoksa yerel staging'den (`{RootPath}/covers/{isbn}`) okunup
   R2'ye yazılır + kayıt defterine düşer (adım 1). Kapak bulunursa `CoverIngested(isbn,url)` yayılır
   (Catalog `SetImage`'e köprü); bulunmazsa event yok — ürün placeholder'da kalır (import bloklanmaz).

## Domain kuralları

- Aynı `StorageType`'tan en fazla bir konum (invariant 1).
- En az bir konum — konumsuz FileAsset yok; son konum silinemez (invariant 2).
- ImageName değişmez + tekil (unique index; setter yok — invariant 3).
- `StorageFilePath` = backend key/path (full URL değil); URL config-base ile üretilir → repoint tek yerden.

## Sınır

- **Byte tutmaz** — fiziki bitler `IFileStore` backend'inde (R2); kayıt defteri yalnız ImageName + konum +
  metadata.
- **Katalog künyesi tutmaz** — kapak-özel kalır (xlsx satırı/ürün alanları sızmaz); tek girdi = ISBN + bit.
- Kanallar: internal S2S REST (register/resolve/serve) + broker (083: `ProductAdded` tüket, `CoverIngested` yay).