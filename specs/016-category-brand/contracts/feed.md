# Kontrat: Tedarikçi Feed (016 revizyonu)

Uç: `GET v1/feeds` (Supplier.Api, dış dünya maketi). Kaynak: `Supplier.Api/Datasets/products.json`
(500 kayıt — mevcut 200 korunur, SUP-1201…SUP-1500 eklenir).

## Kayıt şekli

```json
{
  "externalId": "SUP-1001",
  "name": "Samsung Model 1",
  "description": "Samsung ürünü",
  "brand": "Samsung",
  "category": "Elektronik",
  "price": 59.9,
  "stockQuantity": 7,
  "discountCode": null,
  "discountPercent": null
}
```

- `category` YENİ ve opsiyoneldir (`string?`); boş/eksik kayıt reddedilmez → ürün "kategorisiz" işlenir (FR-010).
- Diğer alanlar değişmez. `SupplierProduct` (Api) ve `SupplierFeedRecord` (Gateway wire) aynı alanı kazanır;
  `ToCanonical` alanı `SupplierProductSnapshotReceived.Category`'ye geçirir.
- Dataset güncellemesi: 500 kaydın TÜMÜ kategorili; makul kategori/marka dağılımı (kategorisiz kayıt yok).