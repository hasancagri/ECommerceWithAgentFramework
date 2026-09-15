# Quickstart / Doğrulama: Hosted-CF Ödeme (077)

Amaç: hosted-CF akışını uçtan uca doğrula. Detay = [spec.md](./spec.md), [contracts/](./contracts/), [data-model.md](./data-model.md).

## Ön koşullar

- Sistem Aspire AppHost'tan: `dotnet run --project src/aspire/AppHost/AppHost.csproj`.
- PG (dış DropShop) hosted-payment + callback uçları hazır (ayrı repo); yoksa PG **stub/mock** (sabit HostedUrl + elle callback POST) ile store tarafı doğrulanır.
- Secrets (Payment.Api): `dotnet user-secrets set PaymentOptions:CallbackSecret <s>` + `PaymentOptions:PgBaseUrl <url>` (+ `PaymentOptions:IntentTimeoutSeconds 300`).
- Müşteri agent'ı (Claude Desktop vb.) mcp-gateway'e login'li bağlı; sepette ürün var.

## Domain birim testleri (İLKE VI, test-first — impl'den ÖNCE yeşil olmalı)

```bash
dotnet test tests/Payment.Api.Tests/Payment.Api.Tests.csproj --filter "FullyQualifiedName~PaymentIntent"
```
Kapsam: Create guard (amount>0, id'ler), MarkSucceeded idempotent (2. çağrı no-op), MarkFailed/Expire yalnız Pending'den + Succeeded'den reddet, IsLive canlı/bayat.

## Senaryo doğrulama (canlı / E2E)

1. **Mutlu yol:** agent'ta "ödeme yap" → `start_payment` HostedUrl döner. Hosted sayfada öde → PG callback → sipariş **Confirmed** (`get_orders` ile gör), stok düştü, sepet boş.
2. **Terk:** "ödeme yap" → linke tıklama, ~5 dk bekle → sipariş **Cancelled**, stok değişmedi.
3. **Red:** hosted sayfada başarısız ödeme (sandbox fail kart) → callback Failed → sipariş Cancelled.
4. **Tekrar-öde (canlı link):** "ödeme yap" x2 hızlı → **aynı** HostedUrl + tek sipariş.
5. **Çift callback (idempotency):** aynı `TxRef` callback'i 2x POST → tek Confirmed sipariş, ikinci no-op.
6. **Sahte callback:** geçersiz `X-Signature` ile POST → **401**, sipariş durumu değişmez.
7. **Boş sepet:** sepeti boşalt → "ödeme yap" → hata değil, "Lütfen sepete ürün ekleyiniz."

## Elle callback (PG stub'suz test)

```bash
# raw_body ile HMAC üret, X-Signature header'a koy:
curl -X POST {payment}/internal/payments/callback \
  -H "X-Signature: <hmac-sha256(secret, body)>" \
  -H "Content-Type: application/json" \
  -d '{"TxRef":"<tx>","PgPaymentRef":"pg-1","Status":"Success"}'
```

## Söküm doğrulama

- `grep -rn "ChargePaymentCommand\|PaymentCharged\|\.Charge(" src` → yalnız test/silinen izler kalmamalı.
- `scripts/check-flow-links.sh` → payment/order FLOW.md güncel, silinen tip adı kalmadı.
- `dotnet build` + `dotnet test` yeşil (checkout saga AlreadyCaptured yolu + PaymentIntent testleri).