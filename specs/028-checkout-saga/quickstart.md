# Quickstart / Canlı Doğrulama: Checkout Saga (028)

## Ön koşullar

- `dotnet run --project src/aspire/AppHost/AppHost.csproj` (tüm sistem Aspire'dan; tek servis çalıştırma yok).
- Feed ingest edilmiş ürünler + stok; WebApp'ten login olunabilir durumda.
- Birim testler: `dotnet test` (Order durum geçişleri, saga karar mantığı, RevertCommit idempotency).

## S1 — Mutlu yol (US1)

1. İki farklı ürünü sepete ekle, checkout'u tamamla.
2. Bekle: yanıt hemen döner; Profil → Siparişlerim'de sipariş "Beklemede" rozetiyle listelenir.
3. Sayfayı yenile (saniyeler içinde): rozet "Onaylandı"; sepet boş; Stock'ta OnHand iki ürün için düşmüş.

## S2 — Kısmi hata + telafi (US2)

1. İki ürünlü sepet kur; 2. ürünün rezervasyonunu pgAdmin/Stock üzerinden boz (veya stoğu 0'a çek).
2. Checkout yap. Beklenen: sipariş "İptal" + sebep; 1. ürünün OnHand'i checkout öncesi değere DÖNMÜŞ (RevertCommit).
3. Sepet DURUYOR (temizlenmedi); kullanıcı düzeltip tekrar deneyebilir.

## S3 — Watchdog / servis çökmesi (US3)

1. Aspire panelinden Stock.Api'yi durdur. Checkout yap.
2. Beklenen: retry'lar tükenir veya watchdog (varsayılan 120 sn) dolar; sipariş "İptal" (zaman aşımı/stok erişilemez sebebi).
3. Restart senaryosu: checkout sonrası Order.Api'yi hemen durdur-başlat; saga kaldığı yerden biter (Pending asılı kalmaz).

## S4 — Pivot-sonrası sepet hatası (US4)

1. Basket.Api'yi durdur; stoklu ürünle checkout yap.
2. Beklenen: sipariş "Onaylandı" KALIR; logda sepet temizliği retry+hata izi; sipariş iptal olmaz (SC-005).

## S5 — Idempotency kanıtı (SC-006)

- Birim test: aynı `orderId` ile `Commit`/`RevertCommit` iki kez → OnHand tek işlem kadar değişir.
- Canlı (opsiyonel): S3 restart senaryosunda çift teslim edilen adım mesajının stok toplamını bozmadığını OnHand'den doğrula.

## Beklenen kalıcı izler

- `orderDb`: Order belgesi (Pending→Confirmed/Cancelled), CheckoutSaga belgesi (tamamlanınca silinir), wolverine zarf tabloları.
- `stockDb`: ProductStock.Quantity değişimi + işlenmiş operasyon anahtarları.
- Kontratlar: [contracts/stock_reservation_changes.md](contracts/stock_reservation_changes.md), [contracts/basket_clear.md](contracts/basket_clear.md).