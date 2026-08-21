# Kontrat: Storefront Filtre API Genişlemesi (043)

Mevcut uçlar genişler; kırıcı değişiklik yok. Tümü anonim okuma (mevcut duruş).

## GET /api/v1/storefront/filters

Yanıta yeni bölüm:

```json
{
  "categories": [ "...mevcut..." ],
  "brands": [ "...mevcut..." ],
  "specifications": [
    { "name": "Renk", "options": [ { "name": "Siyah", "count": 4 }, { "name": "Beyaz", "count": 2 } ] },
    { "name": "Materyal", "options": [ { "name": "Çelik", "count": 3 } ] }
  ]
}
```

- Yalnız yayındaki (silinmemiş, adlı/fiyatlı) satırlardan türetilir.
- `count` = o (attribute, option) çiftini taşıyan ürün sayısı; SC-006 birebirlik şartı.
- Hiç spec verisi yoksa `specifications: []` — WebApp bölümü gizler.
- Cache: mevcut `filters` tag'i (60 sn + ProductChangedEvent invalidation) aynen kapsar.

## GET /api/v1/storefront/products (liste)

Yeni query parametresi (çoklu): `spec=Renk|Siyah&spec=Renk|Beyaz&spec=Materyal|Çelik`

- Biçim: `Attribute|Option` (tek `|` ayracı; adlarda `|` bulunmaz — seed kuralı).
- Semantik: aynı attribute'un değerleri VEYA, farklı attribute'lar VE (FR-008).
- Geçersiz/ayrıştırılamayan `spec` değeri YOK SAYILIR (sorgu yine yanıtlanır).
- `categoryId`/`brandId`/`page` mevcut parametrelerle serbestçe birleşir.

## GET /api/v1/storefront/products/{id} (tekil)

Yanıta: `"specs": [ { "attribute": "Renk", "option": "Siyah" } ]` — detay tablosu; boş liste meşru.

## WebApp query-string sözleşmesi

`/Products?spec=Renk|Siyah&spec=Materyal|Çelik&categoryId=...&page=2` — URL paylaşılabilir/
yenilenebilir (spec edge case); WebApp bu paramları Storefront çağrısına birebir geçirir.
