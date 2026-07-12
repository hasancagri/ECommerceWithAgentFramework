# Contracts: Product Query Behaviour (Completeness Gating)

Bu feature yeni endpoint/DTO eklemez; mevcut sorguların **davranış kontratını** değiştirir.
Aşağıdakiler gözlemlenebilir sözleşmelerdir (uç noktalar ve scope'lar aynı kalır).

## 1. Müşteri/asistan keşif — satılamaz ürünler görünmez

Etkilenen: `Agent/SearchProducts`, `Agent/GetProduct`, `Queries/GetProductByName`
(scope: `CatalogRead`).

**Kontrat:**
- GIVEN bir ürün `IsComplete == false` (açıklama veya görsel boş/whitespace) VEYA `IsActive == false`
- WHEN bu sorgulardan biri ada göre eşleşme arar
- THEN o ürün sonuçta **dönmez** (yokmuş gibi davranılır).

- GIVEN bir ürün `IsActive == true && IsComplete == true`
- WHEN ada göre aranır
- THEN ürün sonuçta döner.

**Değişim özeti:** WHERE koşullarına `IsActive` (eksikse) ve `IsComplete` eklenir.
`GetProductByName` bugün `IsActive`'i de filtrelemiyordu; bu sözleşme onu da ekler.

## 2. Admin listeleme — hepsi görünür, durum ayırt edilebilir

Etkilenen: `Queries/GetAllProducts` (scope: `CatalogRead`).

**Kontrat:**
- GIVEN eksik ve tam ürünlerin karışımı
- WHEN admin tüm ürünleri listeler
- THEN yanıt **her iki türü de** içerir; her öğe için `IsActive`, `IsComplete` ve
  `IsOnSale` (= IsActive && IsComplete) alanları bulunur ve satılabilirlik ayırt edilebilir.

**DTO değişimi (`GetAllProducts.ProductResponse`):**
```
+ bool IsComplete   // açıklama VE görsel dolu mu
+ bool IsOnSale     // IsActive && IsComplete
```
(Mevcut `IsActive` korunur.)

## 3. Doğrudan detay — değişmez

Etkilenen: `Queries/GetProductById` (scope: `CatalogRead`).

**Kontrat:** Davranış değişmez; id ile kayıt çekilir (satılabilirlik filtresi uygulanmaz).
Gerekçe: research Decision 3.

## Aggregate davranış sözleşmesi (write tarafı)

`Product` üzerinde:
- `Create(...)`, `Update(...)`, `UpdateImageUrl(...)` çağrıları sonrası `IsComplete`
  daima `!IsNullOrWhiteSpace(Description) && !IsNullOrWhiteSpace(ImageUrl)` değerine eşittir.
- `IsComplete` dışarıdan yazılamaz (private set); yalnızca yukarıdaki davranışlarla değişir.
- `IsOnSale` daima `IsActive && IsComplete`.