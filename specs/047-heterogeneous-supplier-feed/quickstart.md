# Quickstart — Canlı Doğrulama

Amaç: heterojen feed'in tek iç modele indiğini + buy-box sökümünün fiyat/stoku tek kanaldan akıttığını
+ tek-gate idempotency'yi kanıtlamak. Sistemi Aspire AppHost'tan başlat.

## Ön koşul

```bash
dotnet build
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Procurement OpenAI secret'ı ayarlı (enrich için; söküm AI'ya dokunmaz).
- İki dataset: `supplier-a.json` (A-şekli), `supplier-b.json` (B-şekli, gtin/title/cost/warehouseQty).
  ÖRTÜŞEN barkod YOK.

## Senaryo 1 — Heterojen çekim → tek iç model (US1)

1. Feed pull tetikle (Hangfire ilk koşu veya manuel pull ucu).
2. `GET /v1/pool-products/{barcode}` (Procurement okuma penceresi) ile A ürününü ve B ürününü çek.
3. **Bekle**: B'nin `gtin` değeri barkoda, `cost` fiyata, `warehouseQty` stoğa, `categoryPath` kanonik
   kategoriye çevrilmiş; A birebir. İki ürün de aynı kanonik alanlara oturmuş.
4. `GET` Catalog/Storefront ürün → her ikisi vitrinde, fiyat/stok doğru.

## Senaryo 2 — Adapter izolasyonu (US3)

1. `supplier-b.json`'u bozuk/tanınmaz bir gövdeyle değiştir.
2. Pull çalıştır.
3. **Bekle**: supplier-b çekimi hata loglar + atlanır; supplier-a çekimi eksiksiz sürer (SC-002).
   Dosyayı düzelt, pull tekrar → B geri gelir.

## Senaryo 3 — Fiyat/stok tek kanal (US2)

1. `supplier-a.json`'da bir ürünün `price` ve `stock`'unu değiştir (dosya istek anında okunur).
2. Pull çalıştır.
3. **Bekle**: Catalog fiyatı ve Stock miktarı güncellenir — tek `CanonicalProductUpserted` ile; hiçbir
   buy-box olayı yok. Log'da `BuyBoxChanged` görünmez.
4. **Kimlik sabitliği (SC-007)**: güncelleme öncesi/sonrası aynı barkodun Catalog `Product.Id`'si (ve
   storefront URL'si / bağlı yorumları) DEĞİŞMEZ — yalnız fiyat/stok döner. `GET` ile Id'yi iki kez
   karşılaştır: eşit olmalı.

## Senaryo 4 — Tek-gate idempotency (US2 / SC-008)

1. Dataset değiştirmeden pull'u iki kez çalıştır.
2. **Bekle**: ikinci pull sıfır integration event üretir (Catalog/Stock log sessiz); `TryTakePublish`
   NoChange. Tek publish-gate çalışıyor.

## Senaryo 5 — Delist (US2)

1. `supplier-a.json`'dan bir ürünü çıkar.
2. Pull çalıştır.
3. **Bekle**: kanonik ürün vitrinde KALIR ama stok 0 (satın alınamaz); "rakip kazanır" yolu yok
   (tek tedarikçi). Fiyat son bilinen.

## Söküm doğrulaması (kod tabanı)

```bash
grep -rniE "BuyBoxChanged|EvaluateBuyBox|BuyBoxDecision|ListingChange" src --include='*.cs'   # → sıfır
grep -rniE "advance|Revisions" src/services/supplier --include='*.cs'                          # → sıfır
```

Her ikisi sıfır sonuç vermeli (SC-006, SC-008).

## Domain test kapısı (İlke VI)

```bash
dotnet test tests/Procurement.Api.Tests/Procurement.Api.Tests.csproj
```

PoolProduct tek-listing testleri (merge tek kaynaktan, delist→stok 0, TryTakePublish içerik/fiyat/stok
değişince yayınlar, buy-box yok) yeşil; eski buy-box testleri silinmiş.
