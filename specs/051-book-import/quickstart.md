# Quickstart: First-Party Kitap Toplu Import — Canlı Doğrulama

**Ön koşul:** Docker temiz (reset yapıldı → boş DB'ler). Sistem Aspire AppHost'tan başlar.

```bash
dotnet build
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

## İş1 — books.json üret (build-time, bir kez)

```bash
python3 scripts/book-import/shape_books.py <ham_dataset.json> \
  > src/services/catalog/Catalog.Api/Seeding/Data/books.json
```

**Beklenen:** ≈1427 kayıtlı küçük JSON; her kayıt `isbn/title/brand/priceTry?/imageUrl?/categoryMid/categoryLeaf`.
Ham 20MB dosya repoya commit EDİLMEZ.

## Senaryo 1 — Toplu import + omurga uyanışı (US1, US3)

1. AppHost açılır → Catalog `BookImportHostedService` çalışır (idempotent).
2. **Doğrula (Catalog):** katalog ürün sayısı ≈1427 (fiyatlı yayınlanmış + fiyatsız taslak dahil).
3. **Doğrula (Stock):** yayınlanan her kitap için `ProductStock` OnHand=100 + `BarcodeLink`(ISBN→ProductId) var.
4. **Doğrula (Storefront):** vitrinde yayınlanan kitaplar listelenir; ad/fiyat(TL)/kapak görünür.
5. **Doğrula (WebApp):** ana sayfa/liste kitapları gösterir; kapaksız kitap **placeholder** ile çıkar.

**Geçer:** SC-001 (≈1427 girer), SC-003 (yayınlananlar vitrinde+stokta).

## Senaryo 2 — Fiyatsız taslak kalır (US2)

1. `books.json`'da `priceTry: null` olan bir ISBN seç (≈34 kitaptan biri).
2. **Doğrula (Catalog):** ürün var, `Published=false`.
3. **Doğrula (Storefront):** vitrinde YOK. **Doğrula (Stock):** OnHand YOK (event yayılmadı).

**Geçer:** SC-002 (fiyatsız vitrinde görünmez, %100 kapı).

## Senaryo 3 — Kapaksız yayınlanır (US2)

1. `imageUrl: null` ama `priceTry` dolu bir ISBN seç (≈12 kitaptan biri).
2. **Doğrula:** `Published=true`; vitrinde placeholder görselle görünür; satın-alınabilir.

## Senaryo 4 — Idempotency (US1)

1. Catalog'u yeniden başlat (seeder tekrar çalışır) VEYA seed'i tekrar tetikle.
2. **Doğrula:** katalog kitap sayısı DEĞİŞMEZ (çoğaltma yok). Aynı ISBN → aynı ProductId.

**Geçer:** SC-004 (sıfır çoğaltma), SC-006 (deterministik id).

## Senaryo 5 — Publish gate (domain birim testi, İLKE VI)

```bash
dotnet test --filter "FullyQualifiedName~Product" # Publish() guard testleri
```

**Doğrula:** `Publish()` Price=0'da `PRODUCT_PRICE_REQUIRED_FOR_PUBLISH` hata döner; Price>0'da Ok + Published=true.

## Regresyon — silinen seeder'lar

- **Doğrula:** catalogDb'de Elektronik/Moda kategorisi YOK (CatalogTaxonomySeedHostedService silindi).
- **Doğrula:** spec-attribute (Renk/Beden) seed'i YOK (CatalogSpecSeedHostedService silindi).
- Kategoriler yalnız kitap türlerinden (Literature & Fiction > Genre Fiction...) türemiş olmalı.