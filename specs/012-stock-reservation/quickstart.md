# Quickstart — Stok Rezervasyonu (Model B) Doğrulama

Feature'ın uçtan uca çalıştığını kanıtlayan senaryolar. Ayrıntı için [spec.md](./spec.md),
[data-model.md](./data-model.md), [contracts/](./contracts/).

## Önkoşullar

- Sistemi **Aspire AppHost** ile başlat: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Test için TTL'i kısalt: Stock.Api `appsettings.Development.json` →
  `"Reservations": { "Ttl": "00:02:00", "SweepCron": "* * * * *" }` (2 dk TTL, dakikalık sweep).
- Bir ürünün stoğunu bilinen değere getir (agent/`set_stock` veya `PUT /stocks/set`), ör. **1 adet**.

## Birim testleri (saf domain)

```bash
dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj
dotnet test tests/Basket.Api.Tests/Basket.Api.Tests.csproj
```

Beklenen kapsam:
- `ProductStock.Reserve`: Available yeterliyse OK; yetersizse `INSUFFICIENT_STOCK`.
- `SetReservedQuantity`: idempotent; sabit `ExpiresAt` (ikinci çağrı yenilemez).
- `Release`/`Commit`: Reserved düşer; Commit OnHand'i düşürür, expired/eksikte `Error`.
- `PurgeExpired`: yalnız süresi geçmişleri döndürür/siler.
- Concurrency: iki Reserve son adede yarışırsa yalnız biri başarılı (SC-001).
- `Basket`: Quantity artır/azalt; `qty=0` satırı çıkarır.

## Senaryo 1 — Son ürün rezervasyonu (US1, SC-001)

1. Stok = 1. Kullanıcı **A** ürünü sepete ekler → `200`, `available: 0`.
2. Kullanıcı **B** aynı ürünü sepete ekler → `400 INSUFFICIENT_STOCK`.
3. `GET /stocks/{id}` → `onHand:1, reserved:1, available:0`.

**Beklenen:** yalnız A tutar; B reddedilir; çift satış yok.

## Senaryo 2 — Adet + üst sınır (US3)

1. Stok = 5. A sepete 3 ekler → `available: 2`.
2. A adedi 5'e çıkarır (`PUT .../quantity` = 5) → `available: 0`.
3. A adedi 6'ya çıkarmayı dener → `400 INSUFFICIENT_STOCK` (adet 5 kalır).
4. A adedi 2'ye düşürür → `available: 3`.

## Senaryo 3 — TTL dolumu + sepet temizliği (US4, FR-006b)

1. Stok = 1. A ekler → `available: 0`.
2. ~2 dk bekle (TTL dolar; sweep çalışır).
3. `GET /stocks/{id}` → `available: 1` (lazy filtre + sweep).
4. A `GET /baskets/user` → ürün **sepette yok** (`ReservationExpired` sildi).
5. B ürünü ekler → `200` (yeniden alınabilir).

## Senaryo 4 — Sipariş Commit'i (US2, SC-002)

1. Stok = 5. A 2 adet sepete ekler (`available: 3`).
2. A ödeme + `POST /orders` → `200`.
3. `GET /stocks/{id}` → `onHand: 3` (Commit düştü), `reserved: 0`, `available: 3`.
4. Sipariş yalnız geçerli rezervasyondaki ürünleri içerir.

## Senaryo 5 — Tedarikçi feed'i stoğu ezmez (Model C, SC-005)

1. Stok = 3 iken A 1 sattı (Commit) → `onHand: 2`.
2. Tedarikçi feed'i aynı ürün için güncelleme yayınlar (ör. StockQuantity=15, yalnız fiyat
   değişmiş olsun).
3. `GET /stocks/{id}` → `onHand: 2` **değişmez** (feed OnHand'i ezmedi). Fiyat/indirim
   güncellenmiş olabilir.

## Senaryo 6 — Fail-closed (FR-018)

1. Stock.Api'yi durdur (veya erişilemez yap).
2. A sepete eklemeyi dener → hata; ürün sepete **eklenmez** (oversell yok).

## UI doğrulaması (WebApp)

- Sepet ekranında ürün için **geri sayım** görünür (`reservationExpiresAt`).
- Ürün/sepet ekranında **"son N adet"** (`available`) görünür; 0 iken "stokta yok".

## Anayasa amendment kontrolü

- `.specify/memory/constitution.md` İlke I gRPC senkron kanalı içerir; **v1.2.0**'a
  yükseltilmiş olmalı (implement bunu ratifiye etmeden tamamlanmaz).