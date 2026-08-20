# Mock Feed API Sözleşmesi — Supplier.Api (041)

Eski `GET /v1/feeds` + `Datasets/products.json` SİLİNİR; yerine rev başına statik JSON dataset gelir
(`Datasets/supplier-{kod}.rev{N}.json` — script-üretimli, commit'li, elle düzenlenebilir). Anonim kalır.

## Uçlar

### GET /v1/feeds/{supplierCode}

- `supplierCode` ∈ { `supplier-a`, `supplier-b` }; bilinmeyen kod → 404.
- Dönen: `List<SupplierFeedRow>` — o tedarikçinin GÜNCEL rev dosyası, tam snapshot (full feed).
- Aynı rev'de her çağrı AYNI dosyayı döner (statik veri; determinizm = idempotency testinin temeli).

```csharp
public record SupplierFeedRow(
    string Barcode,           // zorunlu kimlik; dataset her satırda taşır
    string SupplierSku,       // tedarikçinin kendi stok kodu (örn. "A-00123")
    string Name,
    string? Description,      // ~%10 satırda null (enrich tetiği)
    string Brand,
    string? Category,         // TEDARİKÇİYE ÖZGÜ ad (örn. A: "Elektronik/Telefon", B: "Phones"); ~%10 null
    decimal Price,
    int Stock,
    decimal Weight, decimal Length, decimal Width, decimal Height); // ürün türüne uygun sabit bantlar
```

### POST /v1/feeds/{supplierCode}/advance

- Bellek-içi rev'i +1 yapar; `{ supplierCode, rev }` döner. Restart'ta rev=1'e döner (mock; kalıcılık yok).
- rev artışı SONRAKİ dataset dosyasını devreye alır (rev2 dosyası): bir kısım fiyat değişir (kazanan devri),
  bir kısım stok 0'a iner, bir kısım stok 0'dan dolar; 2501-2503 iki tarafta da stok 0 (kazanansız örnek).
  İçerik alanları rev'le DEĞİŞMEZ (içerik hash'i sabit — yalnız fiyat/stok diff'i).
- Dosyası olmayan rev istenirse mevcut EN YÜKSEK rev dosyasına düşülür (advance taşması güvenli).

## Veri dağılımı (rev=1)

- Barkod uzayı: `8690000000001`..`8690000003000` (EAN-13 görünümlü).
- A: 1..1300 benzersiz + 2501..3000 (çakışan 500) → 1800 satır. B: 1301..2500 benzersiz + 2501..3000 → 1700 satır.
- Çakışan 500'de: ~%45 A ucuz, ~%45 B ucuz, ~%10 eşit fiyat; ~%10'unda en ucuz aday stok 0 (fallback senaryosu);
  ad/açıklama hafif farklı (merge gözlemlenebilir); kategori adları İKİ AYRI taksonomiden.
- Ürün türleri varyantsız (telefon, kulaklık, kahve makinesi, çanta, süpürge...); renk/beden YOK; görsel YOK.
- Kategoriler: kanonik ağaç 6 üst × 2-3 alt (14 alt); A "Üst/Alt" Türkçe yolunu, B İngilizce adını kullanır;
  eşleme tabloları Procurement seed'inde bire bir tanımlı (eşlenemeyen ad kalmaz — guard yolu birim testli).
- Dataset kontratı `tests/Supplier.Api.Tests/FeedDatasetTests.cs` ile doğrulanır (sayılar, çakışma, eksik oranı,
  taksonomi ayrımı, rev2'nin yalnız fiyat/stok değiştirdiği).