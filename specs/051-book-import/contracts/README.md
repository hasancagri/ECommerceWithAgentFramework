# Contracts: First-Party Kitap Toplu Import

Bu feature dış REST kontratı EKLEMEZ (açılış seeder'ı; kullanıcı-akışı endpoint'i yok). Kontratlar:
(1) iç command, (2) integration event (rename), (3) veri dosyası şeması.

## 1. ImportBook command (Catalog iç — VSA slice)

`Domains/Products/Features/Commands/ImportBook.cs`. Seeder her kitap için `IMessageBus.InvokeAsync` ile çağırır.

```csharp
record ImportBookCommand(
    string Isbn,          // kimlik; ProductId deterministik türetilir; Gtin+Sku+barkod
    string Title,
    string Brand,         // dataset brand alanı verbatim
    decimal? PriceTry,    // null = fiyatsız → taslak
    string? ImageUrl,
    string CategoryMid,
    string CategoryLeaf);

// Response: { Guid ProductId; bool Published }
```

**Handler davranışı (idempotent):**
1. ProductId = deterministik GUID(Isbn).
2. Brand get-or-create (NormalizedName) → BrandId.
3. Category: mid get-or-create (parentId=null) → leaf get-or-create (parentId=mid) → leaf.Id.
4. Product load(ProductId); yoksa `Create` + Id ata, varsa güncelle (Name/Sku/Gtin/Price/Image/Brand/Category).
5. Price = `Money.Create(PriceTry ?? 0)`.
6. `Publish()` çağır: başarılıysa (Price>0) `Published=true`; hata (Price=0) → Draft.
7. `session.Store(product)`.
8. **Yalnız Published ise** event yay:
   - `ProductAdded(Barcode=Isbn, ProductId, InitialStock=100)`
   - `ProductChangedEvent(ProductId, Name, "", Price.Amount, BrandId, Brand, CategoryId, CategoryLeaf, ImageUrl, IsDeleted:false)`
9. Result: `FeatureObjectResultModel` (Ok/Error).

## 2. ProductAdded integration event (rename: ProductLinked → ProductAdded)

`Shared.IntegrationEvents`:

```csharp
// ÖNCE: record ProductLinked(string Barcode, Guid ProductId, int InitialStock);
record ProductAdded(string Barcode, Guid ProductId, int InitialStock);
```

`RabbitMqConstants`:
```
class ProductAdded {
  Exchange = "catalog.product-added"          // ÖNCE catalog.product-linked
  Queues.Stock = "stock.product-added"        // ÖNCE stock.product-linked
}
```

**Yayıncı:** Catalog (`ImportBook` handler). **Tüketici:** Stock (binding'i tüketici kurar — 007 dersi).
Anlamsal değişim yok; yalnız ad (feed-kaynaklı "Linked" first-party'de anlamsız). Kuyruk adı değiştiği için
reset sonrası eski kuyruk kalıntısı olmaz (temiz DB).

## 3. books.json şeması

Bkz [data-model.md](../data-model.md). Alanlar: `isbn, title, brand, priceTry?, imageUrl?, categoryMid, categoryLeaf`.
`src/services/catalog/Catalog.Api/Seeding/Data/books.json` altında commit'li; ham 20MB dataset repoda DEĞİL.