# Quickstart: Hosted Merchant Onboarding (078) — Canlı Doğrulama

Ön koşullar: her iki sistem ayakta —
`dotnet run --project src/aspire/AppHost/AppHost.csproj` (store) ve PG repo'sunda aynı komut
(PG bu kontratı implemente etmiş olmalı: [contracts/pg-onboarding-rest.md](contracts/pg-onboarding-rest.md)).
Claude Desktop admin bağlantısı: `/mcp-admin` (external-admin-agent).

## S1 — PII'siz başvuru (US1)

1. Admin agent: "PG'ye merchant kaydı başlat, e-posta merchant@test.dev".
2. Bekle: sohbete YALNIZ hosted form linki düşer; agent hiçbir PII istemez.
3. Linki tarayıcıda aç, formu doldur (sandbox verisi), gönder.
4. Doğrula: PG Admin ekranında başvuru Pending; sohbet transkriptinde TCKN/IBAN YOK (SC-001).

## S2 — Approve + tek kullanımlık teslim (US2, PG tarafı kabulü)

1. PG Admin başvuruyu approve eder.
2. Doğrula: Mailpit'te (PG) teslim maili; ilk açılışta MerchantId+Key görünür; sayfayı yenile/tekrar
   aç → görünmez (SC-003).

## S3 — Store ekranından giriş + anlık doğrulama (US3)

1. Admin agent: "PG için merchant bilgilerimi güncelle".
2. Bekle: sohbete yalnız store ekran linki düşer (süreli + tek kullanımlık).
3. Ekranda MerchantId+Key'i yapıştır, kaydet → "kaydedildi + doğrulandı".
4. Negatifler: yanlış key → anında hata (FR-013); aynı linki ikinci kez kullan → nötr 404 sayfası;
   süresi geçmiş link → aynı.
5. Doğrula: hosted ödeme akışı (sepet → ödeme linki) yeni credentials ile çalışır (SC-004);
   AdminActionLog'da `merchant_credentials_submitted` var, içinde MerchantKey YOK (FR-007).

## S4 — Söküm sonrası yüzey (US4) — S1-S3 canlı PASS SONRASI

1. `/mcp-admin` tool listesini çek.
2. Doğrula: PII isteyen tool yok; `admin_set_merchant_credentials` yok; `admin_onboarding_status`
   yanıtında MerchantKey alanı yok (SC-005); Approved durumunda yanıt mail+ekran yoluna yönlendirir.

## S5 — PG kapalıyken dostane hata (FR-011)

1. PG AppHost'u durdur.
2. Admin agent'tan başlatma + durum + ekrandan kayıt dene.
3. Doğrula: teknik detay sızdırmayan "şu an yapılamıyor" mesajları; ekran kaydı `Unverified`
   işaretiyle saklanır ve ekran bunu söyler.

Test sonrası: sandbox MerchantKey rotate et (memory notu — merchant-onboarding-llm-pii-design-debt).