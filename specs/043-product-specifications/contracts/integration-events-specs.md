# Kontrat: Integration Event Specs Genişlemesi (043)

`Shared.IntegrationEvents` — ADDITIVE değişim; eski tüketiciler kırılmaz (yeni alan yok sayılır /
boş liste). Sözleşme AD üzerindendir (Id taşınmaz) — kanonik taksonomi deseni.

## Yeni record

```csharp
public record ProductSpec(string Attribute, string Option);
```

## CanonicalProductUpserted (Procurement → Catalog)

- Yeni alan: `List<ProductSpec> Specs` (default boş).
- İçerik: attribute-başına priority-merge + kapalı-liste enrich sonrası KANONİK adlar.
- Boş liste meşru: özelliksiz ürün yayını engellenmez (FR-005).

## ProductChangedEvent (Catalog → Storefront)

- Yeni alan: `List<ProductSpec> Specs` (default boş).
- İçerik: Product'ın atama listesi, Catalog registry'sinden adlara çözülmüş halde.
- Storefront satırı listeyi sorgulanabilir tutar (facet + filtre + detay).

## Sıralama/teslim

- Mevcut kuyruklar değişmez (`catalog.procurement-events`, Storefront aboneliği); yeni exchange yok.
- Specs alanı hash-diff'e dahil: yalnız attributes değişse bile yeni yayın tetiklenir.
