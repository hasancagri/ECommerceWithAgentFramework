# Kontrat: Integration Event FamilyCode Genişlemesi (045)

`Shared.IntegrationEvents` — ADDITIVE; eski tüketiciler kırılmaz (default null = ailesiz).
Yeni exchange/kuyruk YOK; mevcut yayın koşulları geçerli.

## CanonicalProductUpserted (Procurement → Catalog)

- Yeni alan: `string? FamilyCode = null`.
- İçerik: alan-bazlı Priority-merge sonrası kanonik kod; null = hiçbir tedarikçi vermedi.
- Hash-diff'e dahil: yalnız familyCode değişse bile yeniden yayın tetiklenir (SC-005).

## ProductChangedEvent (Catalog → Storefront)

- Yeni alan: `string? FamilyCode = null`.
- İçerik: Product'taki güncel kod; null geldiğinde tüketici alanı TEMİZLER (aileden çıkış).

## Anlambilim

- FamilyCode OPAK gruplama kimliğidir; tüketici içeriğinden anlam çıkarmaz.
- Aile üyeliği event-başına ürün seviyesindedir; "aile event'i" yoktur — üyeler bağımsız akar,
  liste gruplaması okuma anında `coalesce(FamilyCode, ProductId)` ile kurulur.
- Sıralama: mevcut Sequential kuyruklar yeter (aynı barkod sıralı); üyeler-arası sıra önemsiz
  (her satır yalnız kendi alanını yazar).
