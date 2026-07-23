# Quickstart: 006-home-storefront-list canlı doğrulama

## Önkoşullar

- Docker çalışıyor (Postgres + RabbitMQ Aspire ile kalkar).
- Eski (fiyatsız) vitrin satırları için dev veri sıfırlama kabulü: `storefrontDb` volume'u temizlenebilir olmalı.

## Kurulum ve çalıştırma

```bash
dotnet build
dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Aspire panelinden `storefront-api`, `catalog-api`, `webapp`, `rabbitmq` resource'larının Running olduğunu doğrula.

## Senaryo 1 — Vitrin tek kaynaktan dolar (US1 / SC-001, SC-003)

1. WebApp ana sayfasını aç.
2. Kartlarda ad, açıklama, marka, fiyat ve görselin eksiksiz geldiğini doğrula.
3. Ağ sekmesinde ürün listesi için tek çağrı olduğunu, kart başına ek stok/indirim çağrısı olmadığını doğrula.
4. Çapraz kontrol: `GET http://localhost:<storefront-port>/api/v1/storefront/products` gövdesi kartlarla birebir eşleşmeli.

## Senaryo 2 — Değişiklik kendiliğinden yansır (US2 / SC-002)

1. WebApp'ten (veya Catalog API'den) bir ürünün fiyatını güncelle.
2. 5 sn içinde ana sayfayı yenile; kartta yeni fiyatı gör ([contracts/product-changed-event.md](contracts/product-changed-event.md)).
3. Ürünü sil; kartın listeden düştüğünü doğrula (FR-006).

## Senaryo 3 — Stok ve indirim rozetleri (US3 / FR-009)

1. Stoklu + indirimli bir ürünün kartında stok durumu ve indirim oranını gör.
2. `PUT /api/v1/stocks/set` ile stoğu 0 yap; kartta "stokta yok" rozetini doğrula.
3. Stoğu hiç raporlanmamış (yeni satır) üründe rozet çizilmediğini doğrula.

## Senaryo 4 — Boş ve kısmi vitrin (US1-AS2, FR-005)

1. `storefrontDb` boşken ana sayfa "ürün bulunamadı" durumunu hatasız göstermeli (`200` + `[]`).
2. Yalnız stok raporlanmış (Catalog'u gelmemiş) satırın listede görünmediğini doğrula.

## Senaryo 5 — Regresyon (FR-008 / SC-004)

- Ürün detay, sepete ekleme ve sipariş akışlarını uçtan uca bir kez yürüt; davranış değişikliği olmamalı.

## Eski satırların zenginleşmesi

Fiyatsız eski satırlar için: AppHost'u durdur, Postgres volume'unu sıfırla, sistemi yeniden başlat ve supplier ingestion'ın koşmasını bekle.
Kod tarafında backfill yoktur (spec varsayımı).

Veri-kayıpsız alternatif (canlı doğrulamada kullanıldı): her ürüne aynı değerlerle no-op `PUT /api/v1/products` at.
Update her koşulda fat event yayınlar; satırlar reset'siz zenginleşir.