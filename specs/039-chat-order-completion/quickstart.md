# Quickstart / Doğrulama: Chat Order Completion (039)

Uçtan uca doğrulama rehberi. Kod değil senaryo; ayrıntı için [contracts/](./contracts/) +
[data-model.md](./data-model.md).

## Önkoşullar

- Sistem Aspire ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- PG (ayrı repo) çalışır + **yapısal charge + retrieve-by-key** uçları açık (dış bağımlılık, R5).
- Kullanıcı giriş yapmış, sepette ≥1 ürün, Wallet'ta ≥1 kayıtlı kart + AddressBook varsayılan adres.
- ChatAgent OpenAI config'i hazır; `place_order` allowlist + prompt kuralı yüklü.

## Domain birim testleri (test-first, İlke VI — implementasyondan ÖNCE)

- `PaymentAttemptSaga` `On*`: OnChargeResult(success/failed/ambiguous), OnReconcileTick
  (success/failed/pending/deadline), idempotent re-entry. Mock'suz, xUnit + Shouldly.
- `CorrelationKey.Create`: aynı sepet+taksit → aynı key; sepet değişince farklı key; deterministik
  yeniden hesap.
- Order invariant'ları (mevcut) korunur.

## Senaryo S1 — Mutlu yol (US1)

1. Chat: taksit sorgusu yap (mevcut), kartı seç, "siparişimi tamamla" de.
2. **Bekle**: `place_order` çağrılır → sunucu charge (PG) → Order Confirmed → sepet boşalır.
3. **Doğrula**: chat yanıtında sipariş kodu + özet; `get_orders` yeni siparişi Confirmed gösterir;
   stok düşmüş; sepet boş.

## Senaryo S2 — Ödeme doğrulama geçidi (US2)

- **S2a** (başarısız ödeme): PG `status=failed` → sipariş oluşmaz, kullanıcıya doğrulanamadı mesajı.
- **S2b** (tutar uyuşmaz): PG price ≠ sepet toplamı → red.
- **S2c** (başka kullanıcı): başka buyer'a ait ödeme → red.
- Her üçünde de **sipariş oluşmaz** (SC-003).

## Senaryo S3 — Idempotency (US3, SC-004)

1. "siparişimi tamamla" iki kez (aynı sepet+taksit) tetikle.
2. **Doğrula**: tek çekim (PG dedupe, correlation-key), tek sipariş (paymentId idempotency); çift
   stok düşümü yok.

## Senaryo S4 — Çekim başarılı ama yanıt kayıp (US4)

1. PG charge'ı başarılı yap ama Order.Api'ye yanıtı "kaybettir" (timeout/kesinti simülasyonu).
2. **Bekle**: PaymentAttemptSaga `Unknown` → reconcile tick'leri (backoff) → PG retrieve(key) success
   → sipariş oluşur.
3. **Doğrula**: çift çekim YOK (retry key ile dedupe); tek sipariş; kullanıcıya asla kesin "başarısız"
   denmedi ("kontrol ediliyor / alınmış olabilir").
4. **S4b** (deadline): retrieve sürekli pending kalsın → deadline sonrası terminal
   `NeedsReconciliation`; ops görünürlük (log/kuyruk); reconcile sonsuz değil (FR-020).

## Senaryo S5 — Kenar durumlar

- Boş sepet → `rejected` / ORDER_BASKET_EMPTY.
- Adres/context yok → `rejected` / ORDER_PAYMENT_CONTEXT_MISSING.
- Basket/Customer/PG erişilemez → fail-closed, sipariş oluşmaz.
- Chat'ten kart ekleme/silme talebi → yapılmaz (FR-013).
- Stok yetersiz (commit): CheckoutSaga telafi (RevertCommit+Cancel); ödeme iadesi kapsam dışı.

## Beklenen ölçütler (spec Success Criteria)

- SC-001 ekransız chat siparişi; SC-003 doğrulanamayanların %100'ü sipariş açmaz; SC-004 çift sipariş
  %0; SC-005 belirsizde %100 para-kaybı-algısız mesaj; SC-006 kalem/buyer/ödeme-güveni LLM'de değil.