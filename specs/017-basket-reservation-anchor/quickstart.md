# Quickstart: 017 Basket Reservation Anchor — Doğrulama Rehberi

Uygulama sonrası özelliğin uçtan uca çalıştığını kanıtlayan senaryolar. Kontrat ayrıntısı için
[contracts/stock-reservation-proto.md](./contracts/stock-reservation-proto.md), model için
[data-model.md](./data-model.md).

## Ön koşullar

- Docker çalışıyor (Postgres + RabbitMQ Aspire tarafından kaldırılır).
- Test için süreleri kısalt (canlı doğrulamada beklememek için):
  - `src/services/basket/Basket.Api/appsettings.Development.json` → `"Basket": { "ReservationDuration": "00:01:00" }`
  - Stock sweep zaten dakikalık (`Reservations:SweepCron`), değişiklik gerekmez.

## Kurulum ve çalıştırma

```bash
dotnet build                                            # derleme temiz
dotnet test                                             # tüm birim testleri geçer
dotnet run --project src/aspire/AppHost/AppHost.csproj  # sistemi Aspire ile kaldır
```

WebApp'e giriş yap (Identity.Server üzerinden) ve sepeti kullan.

## Senaryo 1 — Tek sayaç (US1, SC-001)

1. Boş sepete ürün ekle; sepet sayfasını aç.
2. **Beklenen**: tablo ÜSTÜNDE tek geri sayım banner'ı; satırlarda Reservation sütunu/sayaç YOK.

## Senaryo 2 — Çapa sabitliği (US3, SC-002)

1. İlk ürünü ekle, banner süresini not et.
2. İkinci ürünü ekle; birinci ürünün adedini artır; birinci ürünü sil.
3. **Beklenen**: üç işlemin hiçbiri banner süresini değiştirmez; sayaç kaldığı yerden akar.

## Senaryo 3 — Süre gerçek: topluca dolma (US2, SC-003)

1. 2+ ürünlü sepet kur; sürenin dolmasını bekle (1 dk config).
2. **Beklenen**: banner "Expired" olur; en geç ~2 dk içinde (sweep + event) TÜM satırlar otomatik düşer.
3. Doğrulama: Stock loglarında "Reservation sweep: N süresi geçmiş rezervasyon"; sepet sayfası boş.

## Senaryo 4 — Sıfırlama ve yeniden başlama (US4, SC-004)

1. Sepetteki son ürünü elle sil → banner kaybolur.
2. Yeni ürün ekle → **Beklenen**: banner TAM süreden (config değeri) yeniden başlar.

## Senaryo 5 — Süresi dolmuş sepete ekleme (FR-008)

1. Sepeti kur, süreyi doldur (banner "Expired"), sweep koşmadan hemen yeni ürün ekle.
2. **Beklenen**: eski satırlar düşer, yalnız yeni ürün kalır; banner tam süreden başlar.

## Senaryo 6 — Sipariş akışı değişmedi (FR-014, SC-005)

1. Süresi dolmamış sepetle siparişi tamamla.
2. **Beklenen**: sipariş başarılı; Stock `OnHand` düşer; sepet temizlenir; banner kaybolur.

## Senaryo 7 — Geriye uyumluluk (FR-006)

1. (Kod düzeyi) `Order.Api` istemcisi `expires_at` göndermez; Commit yolu birim/canlı davranışta aynıdır.
2. Stock birim testleri: `expiresAt` null → eski sabit-TTL davranışı birebir.

## Birim test kapsamı (dotnet test ile)

- `BasketTests`: çapa kur/koru/sıfırla, `IsExpiredAt`, `PurgeExpiredItems`, son-satır-silme sıfırlaması.
- `ProductStockTests`: açık `expiresAt` ile yeni/mevcut/expired rezervasyon davranışı; null → eski davranış.