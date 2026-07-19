# Quickstart / Validation: AOP Query Caching

**Feature**: 002-aop-query-caching | **Date**: 2026-07-19

Feature'ın uçtan uca çalıştığını kanıtlayan canlı doğrulama senaryoları. Yeni endpoint yok —
mevcut Catalog okuma endpoint'leri şeffaf biçimde önbeklenir.

## Önkoşullar

- Sistem Aspire ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Redis container Aspire dashboard'da `Running` (L2 doğrulaması için).
- Geçerli bir token (`CatalogRead` scope) — login akışıyla alınır.
- Aspire dashboard'da `catalog-api` metrics/log görünür (hit/miss sayaçları — FR-014).

## Senaryo 1 — İki katmanlı okuma (US1 / FR-002, FR-003, SC-001..003)

1. Ürün listesini iki kez peş peşe iste: `GET {gateway}/catalog/v1/products`.
2. **Beklenen**: 1. istek miss → kaynağa gider, L1+L2 dolar (log: `miss`); 2. istek L1 hit
   (log: `l1-hit`), yanıt gövdesi 1. istekle **birebir aynı** (FR-013).
3. L1 TTL'ini (≤5sn) bekle, hemen tekrar iste. **Beklenen**: L1 boş ama L2 hit → kaynağa
   gidilmez (`l2-hit`), sonuç L1'e geri yazılır (SC-003 = kaynak sorgu 0).

## Senaryo 2 — Yazma sonrası tazelik (US2 / FR-006, SC-004)

1. Bir ürünü oku (`GET .../products/{id}`) → iki katmana alınır.
2. Aynı ürünü güncelle: `PUT .../products/{id}` (veya yeni ürün ekle / sil).
3. Güncellemeden hemen sonra tekrar oku. **Beklenen**: TTL beklemeden **güncel** değer döner;
   `catalog-products` etiketi commit sonrası L1+L2'yi boşalttı (≤5sn içinde — SC-004).
4. Liste için: yeni ürün eklendiğinde sonraki liste okuması yeni ürünü içerir; silinen ürün çıkmaz.

## Senaryo 3 — Declarative / kod yok (US3 / FR-007, SC-005)

1. `GetAllProducts.cs` / `GetProductById.cs` handler gövdelerini incele. **Beklenen**: hiçbir
   önbellek çağrısı yok; yalnız query tipinde `[Cached("catalog-products", 5)]` attribute'u var.
2. Bir komut handler'ını incele (`CreateProduct.cs`). **Beklenen**: gövdede boşaltma kodu yok;
   yalnız `[InvalidatesCache("catalog-products")]` attribute'u var.
3. Attribute'u geçici kaldır → sorgu doğrudan kaynaktan yanıtlanır, başka kod değişmez (FR-008).

## Senaryo 4 — Stampede koruması (FR-009 / SC-006)

1. Önbelleği soğut (TTL bekle veya bir yazma ile boşalt).
2. Aynı sorguyu ~100 eşzamanlı istekle aynı anda tetikle (ör. basit bir yük scripti).
3. **Beklenen**: kaynak (Marten) en fazla **1 kez** sorgulanır; diğerleri tek factory sonucunu paylaşır.

## Senaryo 5 — L2 (Redis) düşerse doğruluk (FR-010 / SC-007)

1. Aspire'da `redis` container'ını durdur.
2. Katalog okumalarını tekrarla. **Beklenen**: okumalar **%100 doğru** yanıtlanmaya devam eder
   (L1 veya kaynaktan); hata oranı artmaz. Redis geri gelince L2 tekrar dolar.

## Ölçüm notu (SC-008)

Aspire dashboard'daki cache hit/miss/eviction sayaçları, ısınmış önbellekte kaynağa giden
sorgunun ≥%90 azaldığını (SC-002) ve L1 tekrar okumasının ≥%80 hızlandığını (SC-001) raporlar.