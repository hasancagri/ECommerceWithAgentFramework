# Quickstart / Doğrulama: 024 A2A Installment Quote

Bu feature'ın uçtan uca çalıştığını kanıtlayan senaryolar. Detaylar için
`contracts/a2a-installment-agent.md` + `data-model.md`.

## Önkoşullar

- Sistem Aspire AppHost ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`.
- Dev sertifikası sağlıklı (bozuksa: `dotnet dev-certs https --clean && dotnet dev-certs https --trust`).
- Giriş yapmış kullanıcı (assistant agent hattı).
- **Uzak A2A PaymentAgent** kontrata uygun ayakta VEYA yerel bir stub/mock A2A sunucu
  (`installment_quote` skill'i sabit tablo döner). Contract-first: uzak taraf yoksa US2
  (graceful-degrade) doğrulanır.

## Senaryo 1 — BIN'li taksit sorgusu (US1, mutlu yol)

1. Kullanıcı giriş yapar, sepete ürün ekler (toplam > 0).
2. Cüzdana en az bir kart ekler ve **default** yapar (BIN yakalanmış olmalı).
3. Chat: **"Default kartımla sepetteki tutar için taksitleri getirir misin?"**
4. **Beklenen:** assistant `get_basket` ile toplamı + default kart BIN'ini alır, uzak
   agent'a `installment_quote(amount, bin)` gönderir, o **bankaya özel** taksit tablosunu
   (banka, taksit sayısı, taksit/ay, komisyon) listeler. Tutarlar toplamla tutarlı (SC-001).

## Senaryo 2 — Boş sepet (FR-004)

1. Sepeti boş kullanıcı taksit sorar.
2. **Beklenen:** assistant uzak agent'ı **çağırmaz**, önce sepete ürün eklenmesini ister.

## Senaryo 3 — Uzak agent kapalı (US2, graceful-degrade)

1. Uzak A2A yapılandırılmamış/erişilemez.
2. ChatAgent başlar; arama + sepet + sipariş akışları **çalışır** (SC-002).
3. Taksit sorusu → "taksit bilgisi şu an alınamıyor" (çökme/exception YOK).

## Senaryo 4 — Default kart yok (FR-002a fallback)

1. Kullanıcının default kartı yok.
2. **Beklenen:** ya BIN'siz genel taksit tablosu döner ya da kart eklemesi istenir
   (uydurma alan yok, FR-003).

## Senaryo 5 — Ödeme niyeti reddi (FR-005, SC-003)

1. Kullanıcı "öde / satın al" der.
2. **Beklenen:** assistant bunun kapsamda olmadığını nazikçe belirtir; uzak agent'a
   **charge/ödeme gönderilmez** (yalnız taksit delege).

## Güvenlik doğrulaması (SERT)

- Uzak agent'a giden HTTP gövdesinde **tam PAN / orta haneler / CVV / token YOK** —
  yalnız `amount`, `currency`, `bin` (ilk 6). Trafiği/logu kontrol et.
- OpenAI çağrı context'inde (LLM'e giden) PAN/CVV/token yok; yalnız BIN + tutar.

## Birim testleri

- `SavedCard`: BIN yakalama + doğrulama (6 hane), BIN'siz karta graceful.
- Intent/delege mantığı test edilebilir yerdeyse: boş sepet → çağrı yok; ödeme niyeti → red.
- (A2A istemci entegrasyonu host gerektirir; saf domain testi SavedCard.Bin'e odaklanır.)