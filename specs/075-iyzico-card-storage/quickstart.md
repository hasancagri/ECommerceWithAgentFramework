# Quickstart: PG Aracılı Kart Saklama — Uçtan Uca Doğrulama

Bu feature'ın çalıştığını kanıtlayan senaryolar. Kod değil, **doğrulama/çalıştırma** rehberi.

## Önkoşullar

- Sistem Aspire AppHost'tan ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- PG (harici DropShop) ayakta + iyzico **sandbox** yapılandırması PG tarafında yüklü (kart uçları:
  add-session/list/delete/charge). PG kart sözleşmesi = `contracts/pg-card-contract.md`.
- Customer.Api PG kart uçları için `PgCardOptions:BaseUrl` set (Options; user-secrets).
- Giriş yapmış müşteri (mcp-gateway `/mcp`, upfront login) — Claude Desktop bağlı.
- iyzico sandbox test kartı (ör. `5528790000000008`, ileri SKT, CVV `123`).

## Senaryo 1 — Kart ekle (US1)  [P1]

1. Claude Desktop: "kart eklemek istiyorum" → `add_card` → `{ addUrl, sessionId }` döner.
2. `addUrl`'i tarayıcıda aç → **iyzico hosted form** görünür.
3. Test kartını gir + onayla. (Mağaza/Claude PAN görmez.)
4. Sohbette "kartlarımı listele" → yeni kart marka+son4 ile görünür.
- **Beklenen:** İlk kartsa otomatik `isDefault:true` (FR-001a). `Wallet.PgUserHandle` yazıldı.
- **Denetim:** Sohbet transkriptinde/loglarda PAN/CVV yok (SC-002).

## Senaryo 2 — Kartları listele (US2)  [P1]

1. "kayıtlı kartlarım" → `list_cards`.
- **Beklenen:** Kart(lar) marka+son4+SKT+alias+`cardHandle` ile; PAN/CVV yok. Kart yoksa boş liste
  (hata değil). Liste PG'den canlı (SC-003).

## Senaryo 3 — Varsayılan yap (US4)  [P2]

1. İki kart ekliyken "şu kartı varsayılan yap" (kullanıcı bir kartı işaret eder).
- **Beklenen:** Seçilen `isDefault:true`, diğeri `false` (≤1 varsayılan).

## Senaryo 4 — Kart sil (US3)  [P2]

1. "şu kartı sil" → `delete_card(cardHandle)`.
- **Beklenen:** Sonraki `list_cards`'ta yok; diğer kart durur. Varsayılansa varsayılan temizlendi.
- **Negatif:** Başka kullanıcının handle'ıyla sil → reddedilir (SC-005/FR-008).

## Senaryo 5 — NON-3D çekim, agent onaylı (US5)  [P3]

1. Sepette ürün + "ödeme yap".
2. Agent tutar + kartın son4'ünü gösterir, onay ister (FR-014).
3. "onaylıyorum" → `place_order(confirmed:true)` → checkout saga → PG NON-3D çekim.
- **Beklenen:** Çekim başarılı, sipariş tamam. **Onaysız** (`confirmed:false`) → çekim başlamaz.
- **Beklenen:** 3DS/OTP ekranı YOK.

## İzolasyon denetimi (SC-006)

```bash
grep -ri "iyzipay\|iyzico\|sandbox-api.iyzipay" src/services --include=*.cs
```
- **Beklenen:** Mağaza kodunda **0** sonuç (iyzico yalnız PG'de).

## Regresyon

- `dotnet test tests/Customer.Api.Tests/...` — Wallet aggregate testleri (PgUserHandle çapası,
  tek-varsayılan, ilk-kart-varsayılan, sil→varsayılan-temizle) yeşil.
- Checkout saga çekim yolu (mevcut) hâlâ pivot=Charge + telafi (handle'larla).