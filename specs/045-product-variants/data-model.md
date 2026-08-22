# Data Model: Ürün Varyantları (045)

Yeni aggregate/tablo YOK — `FamilyCode` (string?, opsiyonel) zincir boyunca akar:
feed satırı → PoolProduct → kanonik → event'ler → Product → StorefrontView.

## Procurement BC (mevcut tiplere alan)

| Tip | Alan | Not |
|---|---|---|
| `ListingRow` (VO) | `FamilyCode` string? | ham tedarikçi değeri; `ComputeContentHash`'e girer |
| `CanonicalContent` (VO) | `FamilyCode` string? | alan-bazlı Priority-merge (dolu değer kazanır); `ComputeHash` + `Equals`'a girer |

- `IsComplete`'e GİRMEZ (ailesiz yayın sürer). EnrichmentAgent FamilyCode ÜRETMEZ (şemada yok;
  aggregate guard'ı da yazımı reddeder — barkod/ölçü/fiyat/stok kuralıyla aynı sınıf).
- Kod değişimi → hash farkı → `CanonicalProductUpserted` yeniden yayın (SC-005 bunun üstünden).

## Shared kontratlar (additive)

- `CanonicalProductUpserted` += `string? FamilyCode = null`
- `ProductChangedEvent` += `string? FamilyCode = null`
- → contracts/integration-events-family.md

## Catalog BC

| Tip | Alan | Not |
|---|---|---|
| `Product` | `FamilyCode` string? | kanonik upsert davranışı yazar; Marten index; publish event'e taşır |

Davranış (mevcut metoda parametre; test-first yalnız YENİ guard varsa — düz atama, guard yok):
kanonik upsert `FamilyCode`'u günceller; null gelirse alan temizlenir (aileden çıkış, US1-4).

## Storefront BC (read model satırı)

| Alan | Tip | Not |
|---|---|---|
| `FamilyCode` | string? | `ApplyCatalog` yazar; null = ailesiz |

**Türetilmiş kavramlar (saklanmaz, sorgu/çekirdek hesaplar):**

- **Aile anahtarı**: `coalesce(FamilyCode, ProductId)` — gruplama/count/facet anahtarı.
- **Temsilci üye**: filtreli küme içinde aile başına; sıra `(stok>0) DESC, Price ASC, ProductId`
  (deterministik; FR-007 + Assumption). Saf çekirdek: `PickRepresentative(rows)` (test-first).
- **VariantCount**: ailenin görünür (dolu-satır) üye adedi; ailesizde 1 (R9).
- **Varyant eksenleri**: `DeriveAxes(members)` — üyeler arasında birden çok DEĞER alan spec
  attribute'ları eksen olur; üyesi eksik attribute o üyede "—" sayılır; hiç eksen yoksa boş liste
  (seçici ad-listesine düşer). Saf statik çekirdek (test-first).

## WebApp görünüm modelleri

- Liste kartı: `VariantCount` (>1 ise "N varyant" rozeti).
- Detay: `FamilyViewModel(Members[], Axes[])` — üye: Id, ad, fiyat, stok, görsel, spec değerleri,
  `IsCurrent`. Boş aile → seçici çizilmez.

## Durum/geçişler

```
feed familyCode dolu   → kanonik FamilyCode (merge) → event → Product/View dolu → gruplanır
feed familyCode yok    → null zincir boyu           → ailesiz davranış (bugünkü)
kod kaldırıldı/değişti → hash farkı → yeniden yayın → satır güncellenir → sonraki listede yansır
üye delist/yayın dışı  → dolu-satır filtresi düşürür → seçici/temsilci adaylığından çıkar
```
