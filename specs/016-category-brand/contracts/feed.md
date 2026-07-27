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

- `category` YENİ; wire'da `string?` (dış veri) ama işleme için ZORUNLU: boş/eksik kayıt ingestion
  CategoryWrite'ta kesilir → retry/DLQ (FR-010, kullanıcı kararı 2026-07-27).
- Diğer alanlar değişmez. `SupplierProduct` (Api) ve `SupplierFeedRecord` (Gateway wire) aynı alanı kazanır;
  `ToCanonical` alanı `SupplierProductSnapshotReceived.Category`'ye geçirir.
- Dataset güncellemesi: 500 kaydın TÜMÜ kategorili; makul kategori/marka dağılımı (kategorisiz kayıt yok).