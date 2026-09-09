# Data Model: 070 Admin MCP Surface

Yeni aggregate YOK — mevcut aggregate'ler (Product, ProductStock, MerchantInformation, Basket, Order)
yeni davranış almaz; agent slice'ları var olan metotları çağırır. Yeni veri = yalnız denetim izi +
seed istemci kaydı.

## AdminActionLog (yeni — Catalog, Stock, Customer BC'lerinde AYRI AYRI)

Salt-append Marten dokümanı; AgentQueryLog (069) ailesi. Aggregate DEĞİL (İLKE II muaf; read-model/iz).

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | doküman kimliği |
| UserId | Guid | işlemi yapan (token'dan) |
| Tool | string | tool adı (McpToolNames sabiti) |
| TargetId | string | hedef kayıt (ürün Id / merchant Id / "-" onboarding) |
| Summary | string | özet değişiklik (ör. "Price 120→95"); SIR İÇERMEZ (merchant key asla) |
| Verdict | enum Executed/Rejected | red de iz bırakır (yetki redleri hariç — onlar endpoint'te düşer) |
| CreatedAt | DateTimeOffset | UTC |

Kural: yalnız YAZMA tool'ları yazar (FR-009); okuma tool'ları iz bırakmaz. BC başına kopya tip =
bilinçli tekrar (ortak paket yok).

## Seed OAuth istemcisi (Identity.Server Config — kod sabiti, DB'ye SeedHostedService yazar)

| Alan | Değer |
|---|---|
| ClientId | `external-admin-agent` |
| Tip | public + PKCE; grant: authorization_code + refresh_token |
| Consent | Implicit (seed istemci; consent ekranı yok) |
| Redirect | Claude callback'leri (claude.ai/claude.com) + loopback (localhost/127.0.0.1) |
| Scope'lar | openid, profile, storefront.read, catalog.write, stock.write, merchant.credentials.write |
| DCR ilişkisi | YOK — DCR yüzeyinden üretilemez; ExternalAgentDefaults DEĞİŞMEZ |

Not: gerçek yetki = istemci tavanı ∩ kullanıcı ROL demeti (030). Admin-olmayan kullanıcı bu
istemciyle girse de yönetim scope'u alamaz.

## Taksit seçeneği (geçici sonuç — kalıcı kayıt DEĞİL)

`QuoteInstallmentsForAgent` yanıt kalemi: `InstallmentNumber (int)`, `TotalPrice (decimal)`.
Kaynak: PG A2A skill yanıtı; vaultToken/buyer/merchantKey YANITA GİRMEZ.

## Var olanlar (değişmez, yalnız yeni yüzeyden erişilir)

- **Product/ProductPriceChange (Catalog):** künye güncelleme + yayın anahtarı + fiyat geçmişi —
  mevcut aggregate metotları; agent slice ikizi çağırır.
- **ProductStock (Stock):** mutlak set + artır/azalt — mevcut metotlar (negatif reddi korunur).
- **MerchantInformation (Customer):** upsert + maskeli durum okuma (MerchantKey yanıtta yok —
  mevcut GetMerchantInformation davranışı).
