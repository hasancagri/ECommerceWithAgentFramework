# Quickstart: 056 Doğrulama Rehberi

## Ön koşul

```bash
dotnet build                                            # çözüm yeşil
dotnet test                                             # tüm testler yeşil
scripts/check-flow-links.sh                             # FLOW.md anchor guard
dotnet run --project src/aspire/AppHost/AppHost.csproj  # tüm sistem (Docker açık)
```

## S1 — Kalıcı sepet (US1)

1. WebApp'te giriş yap, bir kitabı sepete at.
2. Header'da/sepette geri sayım GÖRÜNMEMELİ; sepet sayfasında süre uyarısı olmamalı.
3. `basketDb`'de sepette süre alanı yazılmamalı; `stockDb`'de ayırma oluşmamalı.
4. 6+ dk bekle (eski TTL 5 dk idi) → sepet DOLU kalmalı, otomatik boşalma olmamalı.

## S2 — Checkout düşümü (US2)

1. Stok N olan üründen 1 adetle checkout'u tamamla → sipariş oluşmalı, stok N-1.
2. Stok 1 olan üründen (adet tavanı içinde) 2+ adetle checkout dene → ödeme ALINMADAN sipariş
   iptal; kullanıcı stok-yetersiz sonucu görmeli; stok değişmemeli.

## S3 — Son ürün yarışı (US3)

1. Stoğu 1'e indirilmiş ürünü iki farklı kullanıcı sepete atsın — ikisi de ekleyebilmeli.
2. Sırayla checkout: ilki başarılı, ikincisi stok-yetersiz iptali; stok 0'da kalmalı (eksi yok).

## S4 — Söküm artıkları

1. `grep -ri "reservation" src/ --include="*.cs" --include="*.proto"` → yalnız kasıtlı kalıntı
   (yok denecek kadar) çıkmalı.
2. Stock/Basket loglarında `ReservationExpired`/`SweepReservation` hatası akmamalı (deploy anındaki
   tek-seferlik dead-letter gürültüsü hariç — D2).
3. WebApp'te `/purge-expired` çağrısı kalmamalı (ağ sekmesinde görünmemeli).

## Beklenen test kapsamı

- `Stock.Api.Tests`: Commit doğrudan-düşüm (yeterli/yetersiz/idempotent/eksiye-inmez) — test-first.
- `Basket.Api.Tests`: süre kavramsız yaşam (ekle-bekle-dur), 5 tavanı sürer.
- Silinen: anchor/TTL/purge/sweep testleri.