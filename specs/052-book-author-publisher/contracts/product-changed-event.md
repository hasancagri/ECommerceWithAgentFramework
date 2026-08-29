# Contract: ProductChangedEvent (v2 — yazar/yayınevi)

**Konum:** `src/others/Shared/IntegrationEvents.cs`
**Üretici:** Catalog.Api · **Tüketici:** Storefront.Api (tek) · **Kanal:** `product.changed` exchange (fanout) → `storefront.events` queue.

## Değişiklik türü
**Kırıcı (breaking) — bilinçli.** Tek tüketici, aynı PR'da güncellenir, DB sıfırdan seed. `Brand*` alanları çıkar (ölü alan taşımak dürüstsüz). Additive-default kuralı bağımsız-deploy içindir; burada geçerli değil (bkz research D7).

## Şema (v2)

```csharp
public record ProductChangedEvent(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    List<AuthorRef> Authors,          // YENİ — Brand yerine, çok-yazar (Id+Name)
    Guid PublisherId,                 // YENİ
    string Publisher,                 // YENİ — fat: tüketici lookup yapmaz
    Guid CategoryId,
    string Category,
    string? ImageUrl,
    bool IsDeleted,
    List<ProductSpec>? Specs = null,
    string? FamilyCode = null);

public record AuthorRef(Guid Id, string Name);
// Contributor YOK — yazar-dışı katkıcı kapsam dışı (YAGNI, research D5)
```

## Kurallar
- `Authors` boş olamaz (yayınlanan ürün ≥1 yazar; yazarsız kitap "Unknown" yazara bağlanır — hiç boş liste yayılmaz).
- `Publisher` her zaman dolu (her kitaba yayınevi atanır).
- Fat event: `AuthorRef`/`Publisher` ad taşır → tüketici Catalog'a geri sormaz.

## Etkilenen yayın noktaları (Catalog)
- `ImportBook.cs` (~70) — authors listesi + publisher ile yayınla.
- `CreateProduct.cs` (~86), `UpdateProduct.cs` (~88) — aynı alanlar (ürün-CRUD gelecekte; şimdilik import ana yol).