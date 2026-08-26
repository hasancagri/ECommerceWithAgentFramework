# Quickstart: Checkout Orchestrator Doğrulama

Feature'ın uçtan uca çalıştığını kanıtlayan senaryolar. Detay için `contracts/checkout-messages.md`
+ `data-model.md`. Kod gövdesi burada değil — implementasyon `tasks.md`'de.

## Önkoşullar

- Sistem **Aspire AppHost'tan** kalkar: `dotnet run --project src/aspire/AppHost/AppHost.csproj`.
  Yeni `checkout-orchestrator` + genişletilmiş `payment-api` ayakta olmalı; Rabbit + Postgres hazır.
- Identity.Server HTTPS; `checkout-orchestrator` m2m client seed'i + scope'ları yüklü.
- Alıcı: kayıtlı adres + kart (Wallet token), sepette **rezervasyonlu** (012/014) kalem(ler).
- Domain testleri: `dotnet test` (Payment iki-faz + saga `On*` unit'leri PASS).

## Senaryo 1 — Mutlu yol (US1, SC-001/SC-007)

1. WebApp `Order/Create` → adres+kart seç → "Ödemeyi Tamamla" POST.
2. **Beklenen**: <3 sn içinde "siparişin alındı" onayı + `CheckoutId` (senkron bekleme yok).
3. Arka planda sıra: CreateOrder(Pending) → Authorize → Commit(kalemler) → Capture → Confirm →
   ClearBasket.
4. **Doğrula**: Order `Confirmed`; Stock kalıcı düştü; Payment `Captured`; sepet boş. "Siparişlerim"
   başta Pending, tamamlanınca Confirmed.

## Senaryo 2 — Pivot öncesi telafi (US2, SC-002)

1. İki kalemli sepet; ikinci kalemin rezervasyonunu **commit anından hemen önce** düşür (yarış).
2. **Beklenen**: İlk kalem geri sarılır (RevertCommit, LIFO), Payment `Voided` (Captured DEĞİL),
   Order `Cancelled` + sebep. Sepet korunur. Gerçek para tahsil edilmez.
3. **Idempotency**: Aynı `RevertCommitStockCommand`'ı broker redelivery ile ikinci kez enjekte et →
   stok yalnız bir kez geri eklenir (SC-006).

## Senaryo 3 — Pivot sonrası onaylı sipariş korunur (US3, SC-003)

1. `ClearBasketCommand`'ı başarısız olacak şekilde tetikle (Basket handler geçici hata).
2. **Beklenen**: Payment `Captured` + Order **`Confirmed` KALIR**; ClearBasket sınırlı kez retry;
   tükenirse loglanıp süreç `Completed` (iptal YOK).
3. Pivot sonrası watchdog dolsa bile iptal etmez — yalnız tamamlar/loglar.

## Senaryo 4 — Broker dayanıklılığı (US4, SC-004)

1. Süreç Commit adımındayken **Stock servisini durdur**.
2. **Beklenen**: Komut kuyrukta bekler; orchestrator askıda kalmaz, başka işi bloke etmez. Stock
   dönünce adım tamamlanır, süreç doğru sonuca ulaşır.
3. Kalıcı zehirli mesaj (retry tükendi) → **dead-letter**; hata sınıfına göre telafi/log.

## Senaryo 5 — Orchestrator restart (US5, SC-005)

1. Süreç Capture adımındayken `checkout-orchestrator`'ı öldür + yeniden başlat.
2. **Beklenen**: Saga durumu kalıcı olduğundan süreç kaldığı adımdan devam eder; nihai durum tek ve
   doğru (Confirmed/Cancelled). Bekleyen komutlar yeniden teslimde faz-guard + idempotency ile
   bozulmaz.

## Edge doğrulamaları

- **Rezervasyon TTL checkout öncesi dolmuş** → WebApp guard reddeder, saga doğmaz, auth alınmaz.
- **Çift checkout (aynı sepet 2× POST)** → ikinci istek yeni saga doğurmaz (idempotent başlatma).
- **Authorize başarısız** → Order zaten Pending oluşmuştu; hiç kalem kesinleşmez, Order Cancelled.
- **Tamamlanmış sürece geç mesaj** → no-op.

## Regresyon (İlke I sökümü)

- Order.Api'de `CheckoutSaga` + saga gRPC istemcileri SİLİNMİŞ; `dotnet build` PASS.
- Eski 028 saga koşmuyor (FR-002): tek süreç sahibi = orchestrator.
- FLOW.md: `checkout/FLOW.md` yeni; `order/FLOW.md` + `payment/FLOW.md` güncel;
  `scripts/check-flow-links.sh` PASS.