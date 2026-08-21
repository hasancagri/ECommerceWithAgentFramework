# Kontrat: Tedarikçi Feed `attributes` Alanı (043)

`GET /v1/feeds/{kod}` satır şemasına opsiyonel alan. Eski rev dosyaları alansız geçerli kalır.

```json
{
  "barcode": "8690000000001",
  "supplierSku": "A-00001",
  "name": "Peak Kulaklık 1",
  "...": "mevcut alanlar aynen",
  "attributes": { "Renk": "Siyah", "Garanti Süresi": "2 Yıl" }
}
```

- Anahtar/değer HAM tedarikçi diliyle gelir (supplier-a Türkçe, supplier-b İngilizce:
  `"COLOR": "BLACK"`). Kanonikleştirme Procurement'ın işi — feed sözleşmesi ham kalır.
- Bilinmeyen anahtar tüketici tarafında YOK SAYILIR; satırın diğer alanları normal işlenir.
- Alan yoksa/boşsa: özellik bilgisi yok demektir (eksik → enrich adayı).
- Mock veri kuralı: değerler `Datasets/supplier-{a,b}.rev{N}.json` dosyalarına ELLE eklenir.
