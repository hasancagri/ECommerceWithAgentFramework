# Quickstart: Kişisel Ana Sayfa (054) — Canlı Doğrulama

## Önkoşullar

- Docker çalışıyor (Postgres + RabbitMQ Aspire'dan kalkar).
- Katalog dolu (OpenLibrary seed) — yoksa seed akışı çalışmış olmalı.
- `customer` rolünün rol→scope map'inde `storefront.read` işaretli olduğunu doğrula
  (Identity.Server `/Admin/*` ekranı) — kişisel feed 403 dönerse ilk bakılacak yer.

## Çalıştırma

```bash
dotnet build
dotnet test                                             # saf feed birimi testleri dahil
dotnet run --project src/aspire/AppHost/AppHost.csproj  # tüm sistem
```

## Senaryolar

### S1 — Siparişli kullanıcı kişisel feed görür (US1 / SC-001, SC-002)

1. WebApp'te üye ol / login ol (customer).
2. Bir kategoriden (ör. X) bir kitabı sepete at → checkout'u tamamla (sipariş Confirmed olmalı).
3. ≤1 dk içinde ana sayfayı yenile.
4. **Bekle**: yalnız kişisel liste; her kart X kategorisinden YA DA satın alınan kitabın
   yazarından; satın alınan kitap ve varyant ailesi listede YOK; en fazla 12 kart; yazar
   eşleşmeleri kategori eşleşmelerinden önce.

### S2 — Sinyalsiz kullanıcı boş durum görür (US2 / SC-003)

1. Login'siz (gizli pencere) ana sayfayı aç → ürün kartı YOK; yalnız boş durum mesajı
   (kategori kartları da YOK — 2026-09-01 revizyonu).
2. Yeni üye aç (siparişsiz), login'li ana sayfa → aynı boş durum (fallback liste YOK).
3. Navbar "Tüm Kategoriler" → kategori dizini → `/Products?categoryId=...` listesi açılır.

### S3 — Genel vitrin öğeleri kalktı, gezinme sağlam (US3 / SC-004)

1. Ana sayfada "Öne Çıkan Kitaplar" bölümü ve "Tüm Kitaplara Göz At" YOK.
2. Navbar'da "Tüm Kitaplar" girişi YOK; "Tüm Kategoriler" duruyor.
3. `/Products?categoryId=...`, yazar/yayınevi filtreleri, arama: 054 öncesiyle aynı davranış.

### S4 — Idempotency / kenarlar

1. Aynı kitabı İKİNCİ kez satın al → feed bozulmaz (aynı `UserPurchase` satırı, çift sayım yok).
2. Kullanıcının kategori/yazarlarında almadığı kitap kalmayacak şekilde hepsini al (küçük
   kategoriyle dene) → boş durum + kategori kartları (S2 görünümü).

## API-seviyesi hızlı kontrol (opsiyonel)

```bash
# token'lı (WebApp oturumundan bearer alınarak) — 200 + data[]
curl -H "Authorization: Bearer <token>" https://localhost:<gw>/api/v1/storefront/products/personal-feed
# anonim — 401
curl -i https://localhost:<gw>/api/v1/storefront/products/personal-feed
```

## Guard'lar

```bash
scripts/check-flow-links.sh          # Storefront FLOW.md anchor'ları (UserPurchase eklenecek)
scripts/check-claude-spec-links.sh
```