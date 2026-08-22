# Quickstart: Ürün Yorumları ve Puanlama (044) — Canlı Doğrulama

Uçtan uca kanıt: Confirmed sipariş → yorum yazma (gRPC kanıt) → liste + maske → özet event'i →
Storefront kart/detay yıldızı → ModerationAgent gizleme → özet düşümü.

## Önkoşullar

- Sistem Aspire'dan ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- `reviews-api` resource + `reviewsDb` dashboard'da sağlıklı; OpenAI ApiKey config'te
  (yoksa reviews-api fail-fast — moderasyon testleri için zorunlu).
- Bir customer kullanıcıyla en az bir ürün için TAMAMLANMIŞ checkout (OrderStatus.Confirmed).

## Adımlar

1. **Yetki tabanı** — Identity seed sonrası customer rolünde `reviews.write` var (Admin ekranından
   görülebilir); token'da scope dizide geliyor.
   **DİKKAT (mevcut DB):** rol→scope seed yalnız BOŞ rolü doldurur — var olan customer rolüne
   `reviews.write`'ı Admin ekranından elle işaretle (veya docker reset).
   Reviews.Api user-secrets ister: `dotnet user-secrets set OpenAI:ApiKey <key> --project
   src/services/reviews/Reviews.Api` (+ `OpenAI:Model`, ör. gpt-4o-mini).

2. **Yazma — mutlu yol (US1)** — Confirmed siparişli kullanıcıyla ürün detayında form görünür;
   4★ + metin gönder → yorum listede ANINDA görünür, ad maskeli ("H** D**").

3. **Yazma — redler (US1/SC-001)** — a) siparişsiz kullanıcı: form YOK; doğrudan POST →
   `REVIEW_PURCHASE_REQUIRED`. b) Pending siparişli: red. c) aynı ürüne ikinci yorum:
   `REVIEW_ALREADY_EXISTS`. d) rating 0/6/3.5: `REVIEW_RATING_INVALID`.

4. **Fail-closed (FR-008)** — Order.Api dashboard'dan durdur → yorum gönder →
   `REVIEW_PURCHASE_CHECK_UNAVAILABLE`, yazılmadı; liste OKUNMAYA devam ediyor. Order'ı geri başlat.

5. **Özet yayılımı (US3/SC-002)** — yorum sonrası ≤10sn içinde: ürün kartında yıldız + "(N)";
   detay başlığında ortalama. pgAdmin: StorefrontView satırında RatingAverage/RatingCount dolu.

6. **Anonim okuma (US2)** — çıkış yap → detayda liste + maskeli adlar + rozet görünür; form yok.
   Yorumsuz üründe "henüz yorum yok", kartta rozet yok (SC-004).

7. **Moderasyon — ihlal (FR-011)** — küfürlü yorum gönder → önce görünür (fail-open);
   kuyruk işleyince (~sn'ler) listeden düşer, özet azalır; tek yorumsa rozet tamamen kalkar
   (Count=0 yolu). DB'de Status=Hidden + kategori/gerekçe dolu; kullanıcı yeni yorum AÇAMAZ.

8. **Moderasyon — temiz + eleştirel** — "ürün berbat, tavsiye etmem" gibi küfürsüz olumsuz yorum
   GÖRÜNÜR kalır (ModeratedAtUtc damgalı, Status=Visible).

9. **Regresyon** — vitrin listesi/sepet/checkout akışı değişmedi; 042 davranış logu akıyor;
   yorumsuz ürünlerde hiçbir yüzeyde yıldız yok.

## Beklenen sonuç özeti

| Kontrol | Beklenen |
|---------|----------|
| Satın-alma şartı | Confirmed'sız %100 red + form gizli (SC-001) |
| Tek yorum | ikinci deneme %100 red (SC-005) |
| Maske | ham ad hiçbir yanıtta yok; "H** D**" |
| Özet | ≤10sn kart+detay; elle sayımla fark 0 (SC-002/003) |
| Fail-closed | Order kapalı ⇒ yazma red, okuma sürer |
| Fail-open | moderasyon beklerken yorum görünür; ihlalde otomatik Hidden + özet düşer |

## Testler

```bash
dotnet test tests/Reviews.Api.Tests/Reviews.Api.Tests.csproj   # Review.Create/ApplyModeration + VO'lar (test-first)
```