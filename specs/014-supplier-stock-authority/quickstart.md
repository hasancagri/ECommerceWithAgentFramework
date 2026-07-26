# Quickstart: Tedarikçi Feed'i = Stoğun Tek Otoritesi (canlı doğrulama)

Repo konvansiyonu: entegrasyon davranışı canlı/manuel doğrulanır (birim testi yalnız saf
domain). Aşağıdaki senaryolar spec'in Success Criteria'sını uçtan uca kanıtlar.

## Ön koşullar

- Sistem Aspire ile: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Feed kaynağı: `src/services/supplier/Supplier.Api/Datasets/products.json` (restart'sız yansır)
- Manuel tetik: `POST http://<supplier-gateway>/v1/feeds/pull` (anonim, 202 döner)
- Gözlem: Postgres `stockDb` (Stock) + `catalogDb` (Catalog). Şifre:
  `docker exec <postgres> printenv POSTGRES_PASSWORD`. Stok alanı `ProductStock.Quantity`.

## S1 — Yeni ürünün başlangıç stoğu yalnız StockWrite'tan (US1 / SC-001)

1. `products.json`'a yeni bir kayıt ekle (ör. `SUP-9001`, `stockQuantity: 12`).
2. Feed pull tetikle.
3. **Beklenen**: Catalog'da `Sku=SUP-9001` ürünü oluşur; Stock'ta o ProductId için
   `Quantity=12` kaydı **StockWrite üzerinden** oluşur. Değer feed'e eşittir.

## S2 — Tedarikçi stok değişikliği re-sync (US2 / SC-003)

1. İşlenmiş bir ürünün `stockQuantity`'sini değiştir (ör. `12 → 5`).
2. Feed pull tetikle.
3. **Beklenen**: Stock `Quantity` yeni feed değerine (`5`) eşitlenir (mutlak overwrite).
4. **Değişmemiş kayıt kontrolü**: hiçbir alanı değişmeyen ürün için stok yazımı
   tetiklenmez (snapshot-diff kapısı; SC bazlı, log'da "0 yayın").

## S3 — Stoğa tek yazım yolu (US3 / SC-002)

1. **Kod/akış incelemesi**: `ProductCreatedEvent` yok; Stock'ta ProductCreated aboneliği
   yok; Catalog `ProductCreatedEvent` yaymıyor; `PUT /stock/set` ucu yok.
2. `grep -rn "ProductCreated" src` → yalnız tarihsel yorum/kalıntı yok; stok yazan tek
   yer IngestionAgent StockWrite.
3. **Beklenen**: feed dışında stoğa yazan yol = 0.

## S4 — Oversell koruması (SC-004)

1. Bir ürüne aktif sepet rezervasyonu oluştur (Basket→Stock gRPC ile), OnHand'in altında.
2. Feed'de aynı ürünün stoğunu rezervasyonların altına düşür; pull tetikle.
3. **Beklenen**: OnHand feed değerine iner ama `AvailableAt` 0'a kırpılır; müşteri mevcut
   stoktan fazlasını sipariş **edemez**; durum oversold olarak tespit edilir (checkout güvenli).

## S5 — Çift-teslim idempotency (SC-005)

1. Bir ürün olayını iki kez teslim ettir (ör. gateway snapshot satırını silip re-pull —
   013 quickstart yöntemi) veya kuyruk yeniden teslimi.
2. **Beklenen**: Stok adedi tek işlemeyle **aynı** kalır (mutlak set idempotent); mükerrer
   ürün/stok mutasyonu yok; DLQ boş.

## Regresyon kontrolleri

- **Storefront**: yeni/değişen ürün + stok, ana sayfa/storefront view'inde görünür
  (`ProductChangedEvent` + `StockChangedEvent` korunmuş).
- **Build/test**: `dotnet build` 0 hata; `dotnet test` mevcut domain testleri yeşil.