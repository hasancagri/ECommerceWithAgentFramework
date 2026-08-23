# Supplier — Domain Süreci

**BC ne yapar:** Dış dünyanın (dropship tedarikçileri) **maketidir**. DB'siz, bus'suz; her tedarikçinin
feed'ini KENDİ ucundan, KENDİ yabancı JSON şekliyle döndürür. Veriyi Procurement çeker; burası yalnız aynalar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Her tedarikçi AYRI route'ta yayınlanır.** `v1/feeds/supplier-a`      `(AddFeedGroupEndpointExtension`
   ve `v1/feeds/supplier-b` iki bağımsız uç; tek birleşik feed yok.       ` → GetSupplierAFeed/GetSupplierBFeed)`
2. **Feed istek anında dataset dosyasından okunur.** `Datasets/{code}.json` `(ReadDatasetAsync)`
   deserialize edilip döner; bellekte tutulan durum yok, restart gerekmez.
3. **Her tedarikçi KENDİ yabancı şeklini döndürür (heterojen).** A "yerli"  `(SupplierAFeedRow /`
   sözlük (barcode/name/price/stock), B farklı sözlük                       ` SupplierBFeedRow)`
   (gtin/title/cost/warehouseQty + iç içe ölçü).                           `(SupplierBDimensions)`
4. **Bilinmeyen tedarikçi 404'tür.** Dataset dosyası yoksa uç boş değil,   `(ReadDatasetAsync)`
   NotFound döner — sözleşme = dosyanın var olması.
5. **Feed değişimi = dosyayı ELLE düzenlemek.** Yeni fiyat/stok/ürün, JSON'ı `(Datasets/*.json)`
   düzenleyip bir sonraki çekimde yansır; advance/rev endpoint'i SÖKÜLDÜ (047).

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod/GTIN global tekil.** Tedarikçiler arasında ÖRTÜŞME yok — bir kimlik tek tedarikçide (buy-box söküldü, 047).
- **Feed = otoritenin aynası.** Bu BC gerçeğin kendisi değil, dış dünyanın anlık görüntüsü; iş mantığı YOK.
- **Şekil tedarikçiye özgü.** Normalize etmek Procurement ACL adapter'ının işi; maket yabancı sözlüğü olduğu gibi verir.
- **Veri ELLE düzenlenir.** `Datasets/*.json` kod-üreticisiz; yeni alan hem burada hem Procurement DTO'sunda eklenmezse round-trip'te düşer.
- **DB'siz + bus'suz.** Kalıcılık yok, event yayını yok; tek yüzey = salt-okunur HTTP GET.

## Sınır (bu BC'nin dokunmadığı)

Havuz/merge/enrich/yayın YOK — hepsi Procurement'ta. Sipariş bildirimi, stok düşümü, fiyatlandırma bu makette yok.
