# Quickstart: Agent-Only Söküm — Canlı Doğrulama

Söküm sonrası WebApp'i doğrulama rehberi. Kapsam UI/BFF olduğundan güvence = **derleme** +
**canlı Aspire smoke** (domain-TDD yok, İlke VI). Kararlar: [research.md](./research.md).

## Ön koşul

- 062–065 MCP paritesi merged (sepet/sipariş/ödeme-görüntüleme/adres/yorum/fiyat-alarmı chat'te).
- OpenAI secret'ları set (ChatAgent açılışta fail-fast): `OpenAI:ApiKey` + `OpenAI:Model`.

## Kur + çalıştır

```bash
dotnet build                                            # SC-005: 0 derleme hatası (kırık referans yok)
dotnet run --project src/aspire/AppHost/AppHost.csproj  # tüm sistem (Aspire)
```

## Doğrulama senaryoları

### S1 — Kök = mağaza asistanı (SC-001, FR-001)
1. Temiz tarayıcı → WebApp kökü (`/`).
2. **Beklenen**: Klasik vitrin/ürün-listesi DEĞİL, chat asistanı arayüzü ("mağaza asistanı").
   Vitrin/ürün-listesi ekranı HİÇ açılmaz.

### S2 — Chat üzerinden uçtan uca alışveriş (SC-002, FR-001)
1. Kökteki chat'e: "kitap ara" → sonuç yazışmayla döner.
2. "sepete at" → "sipariş ver" → "siparişimi göster" zinciri agent (MCP) üzerinden yürür.
3. **Beklenen**: Her adım yazışmayla tamamlanır; hiçbir klasik mağaza ekranı açılmaz.
   Kimlik gerektiren adımda agent girişe yönlendirir (anonim chat gezinme çalışır).

### S3 — Admin regresyonsuz (SC-003, FR-006/FR-007)
1. Admin kullanıcıyla giriş (SignIn/OIDC).
2. `/Admin/Products/Index` → ürün listesi; `/Admin/Products/Edit/{id}` → künye/fiyat/stok/yayın
   düzenleme çalışır. `/Admin/Onboarding` → merchant onboarding açılır.
3. **Beklenen**: Login/OIDC davranışı değişmemiş; admin ekranları aynen çalışır.

### S4 — Eski müşteri adresi temiz düşer (SC-004, FR-009)
1. Doğrudan git: `/Products/Index`, `/Basket`, `/Account/Profile`, `/Categories/Index`.
2. **Beklenen**: Ham 500/istisna GÖRÜNMEZ; köke (chat) yönlendirilir (`MapFallback`) ya da temiz 404.
3. Admin/login sayfalarında kırık link/eksik gömü YOK (kaldırılan partial referansı kalmamış).

### S5 — Scope yüzeyi daraldı (SC-005, FR-005)
1. Giriş sonrası access_token scope claim'ini incele (ör. jwt decode / IdP log).
2. **Beklenen**: Yalnız kimlik (openid/profile/email/roles/offline_access) + yönetim
   (catalog.write, stock.write, merchant.credentials.write) scope'ları. Müşteri alışveriş
   scope'ları (basket/order/payment/customer/reviews/library/storefront) YOK.

## Başarı ölçütü özeti

| Senaryo | Success Criteria |
|---|---|
| S1 | SC-001 (%100 chat kök) |
| S2 | SC-002 (%100 yazışmayla alışveriş) |
| S3 | SC-003 (admin regresyonsuz) |
| S4 | SC-004 (kırık link=0, ham 500 yok) |
| build + S5 | SC-005 (0 hata + scope daraldı) |