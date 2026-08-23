# Contract — Integration Events (değişim)

Procurement → Catalog/Stock fanout kontratları. Bu feature bir event SİLER, birinin tüketici kümesini
genişletir. `Shared.IntegrationEvents`.

## SİLİNEN: `BuyBoxChanged`

```csharp
// SİL — buy-box bırakıldı (barkod-başı tek tedarikçi). Fiyat/stok artık CanonicalProductUpserted'ta.
public record BuyBoxChanged(string Barcode, Guid? SupplierId, decimal Price, int Stock);
```

- Yayıncı: Procurement `PublishPoolProduct` → yayın kolu SİLİNİR.
- Tüketici: Catalog `Handle(BuyBoxChanged)` SİL; Stock `Handle(BuyBoxChanged)` SİL.

## KORUNAN (şekil aynı): `CanonicalProductUpserted` — tek güncelleme kanalı

```csharp
public record CanonicalProductUpserted(
    string Barcode, string Name, string Description, string Brand,
    string Category, string SubCategory, string Sku,
    decimal Weight, decimal Length, decimal Width, decimal Height,
    decimal Price, int Stock,
    List<ProductSpec>? Specs = null, string? FamilyCode = null);
```

- **Anlam değişimi**: artık içerik **VEYA fiyat VEYA stok** değişince yayınlanır (tek publish-gate).
  Şekil DEĞİŞMEZ (Price+Stock zaten taşınıyordu) → additive/kırıcı değil.
- **Yeni tüketici**: **Stock** bu event'e abone olur (kuyruk binding'i EKLER — tüketici bağlar). Handler:
  `BarcodeLink` lookup → `SetQuantity(evt.Stock)` mutlak yaz → `StockChangedEvent`. Link yoksa atla (R4;
  ilk değer `ProductLinked`'ten).
- **Catalog**: mevcut `Handle(CanonicalProductUpserted)` KORUNUR (fiyatı zaten yazıyor; fiyat/stok-only
  olayda idempotent güvenli).

## KORUNAN (değişmez): `ProductLinked`

```csharp
public record ProductLinked(string Barcode, Guid ProductId, int InitialStock);
```

- Catalog → Stock; yalnız YENİ üründe. Barkod→ürün eşlemesi + ilk OnHand. Yarış-kapısı rolü korunur.

## Akış (söküm sonrası)

```
Procurement.PublishPoolProduct
  └─ CanonicalProductUpserted (içerik/fiyat/stok değişince)
       ├─ Catalog: Product upsert (fiyat dahil) → ProductChangedEvent
       │             └─ (yeni ürün) ProductLinked → Stock: BarcodeLink + ilk OnHand
       └─ Stock: BarcodeLink varsa OnHand mutlak yaz → StockChangedEvent
```

Ayrı buy-box olayı yok; fiyat/stok tek kanaldan.
