# Research: Admin Yüzeyinin MCP'ye Taşınması (070)

Tarih: 2026-09-09. Kaynak: kod taraması (3 paralel keşif) + 061/069 spec mirası.

## R1 — Admin MCP uçlarının topolojisi

- **Karar:** Catalog + Stock, İKİNCİ ve KORUMALI bir MCP ucu açar: `MapMcp("/mcp-admin").RequireAuthorization()`
  + kendi RFC 9728 resource metadata'sı (admin scope'larıyla). Mevcut anonim `/mcp` keşif ucu DOKUNULMAZ.
- **Gerekçe:** Anonim uçta Claude Desktop hiç 401 görmez → RFC 9728 challenge tetiklenmez → OAuth akışı
  hiç başlamaz. Korumalı ayrı uç, 061'in basket/order deseninin (endpoint-auth + metadata) aynısı.
- **Customer.Api:** mevcut `/mcp` ZATEN korumalı; merchant tool'ları oraya EKLENMEZ — müşteri DCR
  istemcileri admin tool şemasını görmesin diye Customer da `/mcp-admin` açar (tutarlı topoloji).
- **Alternatifler:** (a) anonim uca admin tool + handler-scope — reddedildi (OAuth keşfi kopar, şema
  herkese görünür); (b) tek merkezî "Admin MCP servisi" — reddedildi (İLKE I: tool sahibi BC'de yaşar).
- **Gateway:** `/mcp-admin/{catalog|stock|customer}` rotaları + PRM (resource metadata) rotaları eklenir
  (mevcut `/mcp/*` rotalarının ikizi).

## R2 — Tool yerleşimi ve desen

- **Karar:** Her tool, sahibi BC'de `Domains/<Agg>/Features/Agents/<X>ForAgent.cs` İZOLE slice +
  `<Agg>McpTools.cs`'te ince `[McpServerTool]` sarmalayıcı (mevcut Basket/Order deseni birebir).
- **Mevcut REST admin dilimleri şablon:** AdminListProducts, AdminGetProduct, UpdateProduct,
  SetProductPublished, GetProductPriceHistory (Catalog); SetStockQuantity (Stock);
  Set/GetMerchantInformation (Customer). Agent slice'ları bunların ikizidir (bilinçli tekrar —
  Commands/Queries'e IMessageBus ile bile gidilmez; docs/conventions.md).
- **Tool adları:** `Shared/McpToolNames.cs`'e yeni sınıflar: `CatalogAdminTools`, `StockAdminTools`,
  `CustomerAdminTools`, `OrderTools.QuoteInstallments` (tek kaynak: attribute + prompt aynı sabiti okur).

## R3 — Yetki: scope'lar hazır, yeni scope GEREKMEZ

- `catalog.write`, `stock.write`, `merchant.credentials.write` KnownScopes/Config.AllApiScopes'ta VAR;
  admin rolü 17 scope'un tamamını taşıyor (Config.AdminRoleScopes = AllApiScopes).
- Admin okuma tool'ları da (liste/detay/geçmiş) ilgili BC'nin write scope'uyla korunur — mevcut REST
  davranışıyla aynı (ProductEndpointExtension admin grubu CatalogWrite ile korunuyor). Yeni okuma
  scope'u üretilmez (kapalı registry şişirilmez).
- `[RequiredScope]` Wolverine handler'larına; endpoint `RequireAuthorization` — çift katman 061 deseni.

## R4 — Seed'li admin OAuth istemcisi

- **Karar:** `Config.Clients`'a `external-admin-agent`: public + PKCE, `authorization_code+refresh_token`,
  redirect = Claude callback'leri + loopback (DcrRequestValidator'la AYNI kümeler ama seed'de sabit),
  `ConsentType=Implicit` (seed istemciler consent görmez; admin = mağaza sahibi), scope demeti =
  `openid profile catalog.write stock.write merchant.credentials.write` (+ mevcut admin okumaları
  için gereken: storefront.read).
- **DCR tavanı DEĞİŞMEZ:** ExternalAgentDefaults'a yönetim scope'u eklenmez; DCR yolundan yönetim
  scope'u istense sessizce düşer (mevcut davranış, SC-006 bunu test eder).
- **Seed mekanizması:** `SeedHostedService` idempotent — yeni istemci oraya eklenir.
- **Token'daki scope gerçeği role bağlı:** authorization_code akışında scope'lar kullanıcının ROL
  demetinden süzülür (030) — admin olmayan biri bu istemciyle girse bile yönetim scope'u ALAMAZ.
  İstemci tavanı + rol süzgeci birlikte çalışır.

## R5 — quote_installments (taksit sorgusu)

- **Karar:** Order.Api `Features/Agents/QuoteInstallmentsForAgent`: PlaceOrderForAgent zincirinin
  quote'a kadar olan aynısı — basket gRPC (toplam) + Customer payment-context S2S (vaultToken,
  merchantId, buyer) → PaymentGateway'in A2A `quote-installments` skill'i **Order.Api içinden A2A
  istemcisiyle** çağrılır. Sonuç: taksit sayısı + toplam listesi; vault token/buyer yanıtta dönmez.
- **Gerekçe:** PG'ye dokunma kısıtı — PG taksit sorgusunu YALNIZ A2A skill'iyle sunuyor (REST quote
  ucu yok; ChatAgent bugüne dek A2A ile soruyordu). A2A servis içinden çağırmak MCP yasağına girmez
  (yasak spesifik MCP'ye; anayasa v1.8.1). Çirkinliği kabul edilmiş teknik borç: PG kısıtı gevşerse
  düz REST'e indirilir (spec Assumptions).
- **Alternatifler:** (a) PG'ye REST quote ucu ekle — reddedildi (kısıt); (b) özelliği öldür —
  kullanıcı reddetti (US4).
- **Paket:** `A2A` paketi Order.Api'ye eklenir (Directory.Packages.props'ta sürüm zaten var).

## R6 — DropShop/PG onboarding sarmalayıcı (FR-016, anayasa sapması)

- **Gerçek:** onboarding MCP'si PaymentGateway repo'sundaki `Merchant.Api /mcp` (merchant.write
  scope'lu, KENDİ realm'i); tool'lar `submit_registration`/`registration_status`. Submit/status için
  REST YOK (PG'deki register-requests REST'i gateway-admin tarafı, merchant başvurusu değil).
- **Karar:** Customer.Api `/mcp-admin`'e 2 sarmalayıcı tool: `admin_submit_onboarding` /
  `admin_onboarding_status`. Handler, PG Merchant.Api MCP'sini S2S **imperatif MCP istemcisiyle**
  çağırır (ChatAgent'ın OnboardingGatewayTokenHandler client_credentials deseni Customer.Api'ye taşınır).
- **ANAYASA SAPMASI (v1.8.1 "MCP'yi yalnız agent tüketir"):** gerekçeli — dış solution bu işlemi
  YALNIZ MCP ile sunuyor ve PG'ye dokunmak yasak; sapma tek slice'a hapsedilir, Complexity Tracking'e
  yazılır. PG bir gün REST sunarsa istemci değişir, tool sözleşmesi değişmez.
- **Neden Customer.Api:** merchant kimliği (MerchantInformation) zaten bu BC'de; onboarding çıktısı
  (MerchantId+Key) bu kaydı besler — süreç sahibi o.

## R7 — Playbook göçü (069 → tool description)

- **Karar:** StorefrontQueryPlaybook metni (şema kolonları + sorgu kalıpları + {{EMBED}} + 0.68 eşiği +
  dürüstlük kuralları) `StorefrontMcpTools.cs`'teki `[Description]`'a taşınır (const interpolasyon —
  Shared/McpToolNames sabitleri kullanılabilir; kolon adları düz metin).
- **Guard:** `check-agent-query-schema.sh` `prompt_file` hedefi → `StorefrontMcpTools.cs`.
- **ChatAgent kopyası DONDURULUR** (silinmez — FR-013 "ChatAgent bozulmaz"; söküm feature'ında ölür).
  Geçici çift kopya bilinçli tekrar; kanonik ev artık tool description.
- **Risk/sınır:** MCP tool description bazı istemcilerde kırpılabilir (çok uzunsa) — göçte metin
  sadeleştirilir (persona-özel satırlar atılır: "kural 8/9" çapraz atıfları, sepete-ekleme yönergeleri
  ChatAgent'a özgü; tool'a yalnız SORGU rehberi gider). Eval (SC-004) bunu doğrular.

## R8 — Denetim izi (audit)

- **Karar:** AgentQueryLog emsali: her yazma-BC'sinde salt-append Marten dokümanı `AdminActionLog`
  (Id, UserId, Tool, TargetId, Summary, CreatedAt). Agent slice handler'ı işlem SONUCUNDA yazar
  (başarı + red ayrımıyla). BC başına ayrı tip (bilinçli tekrar; ortak paket açılmaz).
- **Alternatif:** merkezi audit servisi — reddedildi (İLKE I; god-service).

## R9 — Kayıt (SignUp) ve RBAC ekranları

- Doğrulandı: Identity.Server `Account/Create` mevcut (WebApp yalnız `prompt=create` tetikliyor);
  RBAC ekranları Identity.Server'da. Bu feature'da İŞ YOK — sadece spec varsayımı olarak kayıtlı.
