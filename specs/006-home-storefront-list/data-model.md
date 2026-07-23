# Data Model: 006-home-storefront-list

## StorefrontView (Storefront.Api — Marten dokümanı, aggregate değil)

ProductId-anahtarlı tek composite satır; her kaynak yalnız kendi alanlarını yazar. Optimistic concurrency + tek Sequential kuyruk korunur.

| Alan | Tip | Kaynak | Not |
|------|-----|--------|-----|
| ProductId | Guid | anahtar | Marten Identity |
| Name | string? | Catalog | null = Catalog henüz raporlamadı |
| **Description** | string? | Catalog | YENİ — fat event'ten |
| **Price** | decimal? | Catalog | YENİ — null = fat veri gelmedi (dolu-satır filtresinin işareti) |
| **Brand** | string? | Catalog | YENİ — enum adı string (K2) |
| ImageUrl | string? | Catalog | mevcut |
| IsDeleted | bool | Catalog | mevcut; listede filtrelenir |
| StockQuantity | int? | Stock | mevcut; null = bilinmiyor |
| DiscountRate | decimal? | Discount | mevcut; null = indirim yok/raporlanmadı |
| IsAvailableForSale | bool | ayrı süreç | mevcut; bu feature'da filtrelenmez (FR-007) |

- `ApplyCatalog(name, description, price, brand, imageUrl, isDeleted)` olarak genişler; beş Catalog alanı tek atomik uygulanır.
- Yeni alanlara başka hiçbir kaynak yazamaz.

## ProductChangedEvent (Shared.IntegrationEvents — bilinçli paylaşılan kontrat)

Yerinde genişletilir (K1). Yeni şekil:

```
ProductChangedEvent(Guid ProductId, string Name, string Description,
                    decimal Price, string Brand, string? ImageUrl, bool IsDeleted)
```

- Yayıncılar: `CreateProduct` (IsDeleted=false), `UpdateProduct` (IsDeleted=false), `DeleteProduct` (IsDeleted=true, son değerlerle).
- `UpsertProduct` (agent yüzü) Create/Update'e delege ettiği için otomatik uyumludur; ayrı iş yok.
- Diğer event'ler (`StockChangedEvent`, `DiscountChangedEvent`, `ProductCreatedEvent`) değişmez.

## Doğrulama / iş kuralları

- Dolu-satır (listelenebilirlik) kuralı: `!IsDeleted && Name != null && Price != null` (K7). Sorguda uygulanır, view'da alan tutulmaz.
- `IsInStock` türetilir, saklanmaz: `StockQuantity.HasValue ? StockQuantity > 0 : null` (K8).
- Sıralama: `Name` artan (K6).

## State geçişleri

Satır yaşam döngüsü değişmiyor: herhangi bir kaynak satırı yaratabilir (kısmi satır geçerli), Catalog silmeyi `IsDeleted=true` ile işaretler.
Silinen satır listeden düşer; detay ucu (`GetProductStorefrontView`) davranışı değişmez.