# Mock Feed API Sözleşmesi — Supplier.Api (041)

Eski `GET /v1/feeds` + `Datasets/products.json` SİLİNİR; yerine deterministik üretici gelir. Anonim kalır.

## Uçlar

### GET /v1/feeds/{supplierCode}

- `supplierCode` ∈ { `supplier-a`, `supplier-b` }; bilinmeyen kod → 404.
- Dönen: `List<SupplierFeedRow>` — o tedarikçinin GÜNCEL rev'ine göre tam snapshot (full feed).
- Aynı rev'de her çağrı BYTE-AYNI veri döner (sabit seed; determinizm = idempotency testinin temeli).

```csharp
public record SupplierFeedRow(
    string Barcode,           // zorunlu kimlik; mock her satırda üretir
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
- rev artışı deterministik sapma üretir: bir kısım fiyat değişir (kazanan devri), bir kısım stok 0'a iner,
  bir kısım stok 0'dan dolar. İçerik alanları rev'le DEĞİŞMEZ (içerik hash'i sabit — yalnız fiyat/stok diff'i).

## Veri dağılımı (rev=1)

- Barkod uzayı: `8690000000001`..`8690000003000` (EAN-13 görünümlü, deterministik).
- A: 1..1300 benzersiz + 1801..2300 (çakışan 500) → 1800 satır. B: 1301..1800 benzersiz + 1801..2300 → 1700 satır.
- Çakışan 500'de: ~%45 A ucuz, ~%45 B ucuz, ~%10 eşit fiyat; ~%10'unda en ucuz aday stok 0 (fallback senaryosu);
  ad/açıklama hafif farklı (merge gözlemlenebilir); kategori adları İKİ AYRI taksonomiden.
- Ürün türleri varyantsız (telefon, kulaklık, kahve makinesi, çanta, süpürge...); renk/beden YOK; görsel YOK.
- Kategoriler: kanonik ağaç ~6 üst × 2-3 alt; A ve B kendi ad/dil varyantlarını kullanır; eşleme tabloları
  Procurement seed'inde bire bir tanımlı (eşlenemeyen ad kalmaz — guard yolu birim testle doğrulanır).