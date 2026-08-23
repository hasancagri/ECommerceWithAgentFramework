# Contract — Heterojen Mock Feed API (Supplier.Api)

Tek Supplier.Api process; her tedarikçi AYRI route + AYRI JSON şekli. Datasetler ELLE düzenlenir
(kod-gen mock reddedildi). **`advance`/rev makinesi YOK** (sökülür) — tek dataset dosyası, istek anında
okunur; feed değişimi dosyayı elle düzenleyerek simüle edilir.

## Route'lar

| Method | Route | Döner | Dataset |
|---|---|---|---|
| GET | `v1/feeds/supplier-a` | `SupplierAFeedRow[]` (A-şekli) | `supplier-a.json` |
| GET | `v1/feeds/supplier-b` | `SupplierBFeedRow[]` (B-şekli) | `supplier-b.json` |

Bilinmeyen tedarikçi = route yok (404). POST ucu yok. Yalnız okuma; yan etki yok.

## Şekil A (supplier-a) — mevcut şekli KORUR

```json
{
  "barcode": "8690000000001",
  "supplierSku": "A-00001",
  "name": "Peak Kulaklik Pro",
  "description": "…",
  "brand": "Peak",
  "category": "Elektronik/Kulaklık",
  "price": 4597.21,
  "stock": 3,
  "weight": 0.3, "length": 18, "width": 16, "height": 8,
  "attributes": { "Renk": "Kırmızı" },
  "familyCode": "FAM-KULAKLIK"
}
```

## Şekil B (supplier-b) — YENİ, farklı kelimeler

```json
{
  "gtin": "8690000009001",
  "sku": "B-00001",
  "title": "Nokta Bluetooth Kulaklık",
  "details": "…",
  "manufacturer": "Nokta",
  "categoryPath": "Elektronik > Kulaklık",
  "cost": 512.90,
  "warehouseQty": 40,
  "dimensionsCm": { "w": 0.28, "l": 17, "wd": 15, "h": 7 },
  "specs": { "Renk": "Siyah" },
  "variantGroup": "FAM-KULAKLIK-B"
}
```

**B→nötr eşleme (Procurement adapter'ında):** `gtin→Barcode`, `sku→SupplierSku`, `title→Name`,
`details→Description`, `manufacturer→Brand`, `categoryPath→Category` (`" > "` → `/`), `cost→Price`,
`warehouseQty→Stock`, `dimensionsCm.{w,l,wd,h}→{Weight,Length,Width,Height}`, `specs→Attributes`,
`variantGroup→FamilyCode`.

## Kurallar

- **Barkod global tekil**: supplier-a ve supplier-b datasetleri ÖRTÜŞEN barkod/gtin İÇERMEZ (buy-box
  bırakıldı). Elle garanti; guard KAPSAM DIŞI.
- **Yeni alan**: hem ilgili tedarikçinin feed şekline hem Procurement adapter'ına eklenir; yoksa
  çeviride düşer.
- **Zorunlu kimlik**: barkod/gtin boş satır Procurement adapter'ında/handler'ında reddedilir + loglanır
  (FR-006).
- **Feed değişimi simülasyonu**: dataset dosyasını düzenle → sonraki GET yeni veriyi verir (dosya istek
  anında okunur; restart gerekmez).
