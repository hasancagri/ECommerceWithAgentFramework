# Research: UCP Checkout Kanalı

Phase 0 — teknik bilinmeyenlerin çözümü. Kaynak: kanonik UCP spec repo
`Universal-Commerce-Protocol/ucp` (`source/schemas/*`) + RFC 9421/9530 + mevcut kod.

## R1 — UCP transport seçimi (REST vs MCP/A2A/embedded)

- **Karar**: Kanal **HTTP REST** ile sunulur (checkout-session uçları + `.well-known` keşif +
  giden webhook).
- **Gerekçe**: UCP transport-agnostiktir (`source/schemas/transports/`: a2a_message, embedded,
  jsonrpc, mcp_tool_call), ama referans REST örneği REST'tir; `.well-known` keşif ve imzalı webhook
  zaten HTTP gerektirir; conformance en kolay REST'te kanıtlanır. UCP uçları dış-protokol yüzeyidir,
  iç müşteri MCP-only kuralının kapsamı değil (İlke I/III uyumlu — admin/S2S REST emsali).
- **Alternatifler**: `mcp_tool_call` transport (mağazayı MCP tool'u olarak sunmak) proje MCP-ağırlığına
  cazip; ama keşif/webhook yine HTTP ister ve conformance örneği REST → kapsam dışı, gelecekte ek transport.

## R2 — Kimlik: makine kimliği (client_credentials) vs per-user (authorization_code)

- **Karar**: MVP'de platform **makine kimliği** — `client_credentials` + statik scope
  `dev.ucp.shopping.checkout`; UCP siparişinin kullanıcısı **sentetik** (`UcpPlatform.SyntheticUserId`).
- **Gerekçe**: İlke V makine kimliklerini `client_credentials` + statik scope ile tanımlar (RBAC dışı);
  071'in `acp-gateway` m2m emsali. Sandbox demo için OAuth authorization_code kullanıcı dansı gereksiz
  ağırlık. **ACP'nin API-key sapması burada kapanır** çünkü kimlik gerçek OAuth scope'u.
- **Alternatif**: UCP identity-linking'in tam hali per-user `authorization_code` + consent (061 DCR
  altyapısı hazır) — gerçek müşteri kimliği akar, sentetik kullanıcı ölür. Değer yüksek ama demo için
  fazla; **gelecek feature** olarak işaretlendi (backlog).

## R3 — RFC 9421 (HTTP Message Signatures) .NET uygulaması

- **Karar**: Olgun NuGet olmadığından **elle minimal RFC 9421** uygulanır: signature-base kurulumu
  (`Signature-Input` bileşen sırası), `Content-Digest` (RFC 9530, SHA-256, `sha-256=:...:`),
  algoritmalar **ecdsa-p256-sha256 (ES256)** BCL `ECDsa` ile + **ed25519** `NSec.Cryptography` ile
  (Ed25519 BCL'de yok). Public anahtar profil JWKS'inden `kid` ile çözülür. Zaman/`created` tazeliği
  replay guard.
- **Gerekçe**: RFC 9421 Şubat 2024 standardı; hazır olgun .NET kütüphanesi yok. Sadece iki algoritma
  ve dar bir bileşen kümesi gerektiğinden elle uygulama makul + bağımlılık minimum. Zorlama **opsiyonel
  bayrak** (`UcpSigningOption.RequireSignatures`, sandbox default kapalı) — UCP örneğinin
  `--require_signatures=false` deseni. Giden webhook mağazanın private key'iyle imzalanır.
- **Alternatif**: BouncyCastle (Ed25519 dahil tek bağımlılık) — NSec yerine kullanılabilir; NSec libsodium
  tabanlı, daha dar API. Ağır tam-RFC kütüphanesi reddedildi (kapsam fazlası).

## R4 — Checkout session modeli + operasyonlar

- **Karar**: `source/schemas/shopping/checkout.json`'a uyulur. Zorunlu: `ucp,id,line_items,status,
  currency,totals,links`. Status enum: `incomplete → requires_escalation | ready_for_complete →
  complete_in_progress → completed | canceled`. `expires_at` yoksa **6 saat** default. Update'te
  `line_items` **tam değişim** (replace). `payment` complete'te koşullu; `order` tamamlanınca.
- **Gerekçe**: Kanonik şema; conformance (SC-006) buna bağlı.
- **Operasyon eşleme**: create=`POST /ucp/checkout_sessions`, update=`POST /ucp/checkout_sessions/{id}`
  (tam değişim), get=`GET`, complete=`POST .../{id}/complete`, cancel=`POST .../{id}/cancel`
  (kesin yollar contracts'ta).

## R5 — Fulfillment uzantısı

- **Karar**: `shopping/fulfillment.json` — checkout'a `allOf` ile opsiyonel `fulfillment` alanı;
  `methods` (type: shipping/pickup, `options` → ad + `cost`), `groups` (kalemleri destinasyon+method'a
  göre), `selected_destination_id`. Kargo bedeli `totals`'a yansır. Embedded olaylar
  `ec.fulfillment.change` / `address_change_request` MVP'de basitleştirilir (adres girilince mağaza
  seçenekleri hesaplar).
- **Gerekçe**: Q2=tam kapsam. Kargo tarifesi için mevcut/basit mağaza mantığı (karmaşık motor kapsam dışı,
  spec assumption). Kitapçı kargo notları: [[kargo-shipping-design-decisions]].

## R6 — Discount uzantısı

- **Karar**: `shopping/discount.json` — checkout'a `allOf` ile `discounts` alanı: istekte
  `discounts.codes[]`, yanıtta `discounts.applied[]` (`applied_discount`: `title`,`amount`,
  opsiyonel `code`,`allocations[path,amount]`). Complete'te `discounts` omit. Geçersiz kod
  açıklayıcı `messages` ile reddedilir, session hataya düşmez.
- **Gerekçe**: Q2=tam kapsam. İndirim tutarı `totals`'a yansır. Basit kod-tabanlı indirim (promosyon
  motoru değil).

## R7 — Ödeme: PG üzerinden iyzico sandbox, already-captured borusu

- **Karar**: Complete → sanksiyonlu gRPC `CreateExternalOrder`(Order). **Charge Order içinde** yapılır
  (mevcut chat charge yolu `PlaceOrderForAgent → PaymentGatewayClient.ChargeAsync`), sonra
  `StartCheckout(AlreadyCaptured)` — saga charge pivotu atlanır. UCP BC PG'ye doğrudan dokunmaz.
- **Ödeme aracı**: Dış platform alıcısının Customer BC'de vault kartı YOK. MVP'de UCP siparişleri
  **konfigüre edilmiş iyzico sandbox test kartı/instrument** ile tahsil edilir; UCP payment handler
  profilde bunu ilan eder. Gerçek para yok, 3DS-siz.
- **DOĞRULANACAK dış bağımlılık**: PG şu an iyzico **sandbox**'a mı bağlı + çalışan test key var mı
  (PG ayrı repo, `.slnx` dışı — dokunulmaz). Yoksa PG tarafında sandbox key ayarı ön-koşul.
- **Gerekçe**: Topoloji 2 (mağaza payment handler = PG); "işlemler PG'ye geçsin" kullanıcı kararı,
  "PG'ye dokunma" kısıtı bu feature için bilinçli geçersiz. İlgili: [[ucp-pivot-direction]],
  [[payment-gateway-merchant-key-single-source]].

## R8 — Katalog projeksiyonu (keşif/arama kaynağı)

- **Karar**: `UcpCatalogItem` kendi izole projeksiyonu; `ProductChangedEvent` + stok olaylarından
  beslenir (Storefront push-only deseni), tek `ucp.events` kuyruğu Sequential. catalog_lookup (id) +
  catalog_search bu projeksiyondan.
- **Gerekçe**: İlke I — UCP Catalog'un DB/aggregate'ine erişemez; kendi read modelini olay tüketerek
  kurar. Aynı kavram farklı model.

## R9 — Sipariş olay bildirimi (webhook)

- **Karar**: Order `OrderCompleted` (mevcut fanout) + **additive `OrderCanceledEvent`** tüketilir →
  platforma imzalı webhook (`order.confirmed`/`order.canceled`), retry + exponential backoff, teslim izi.
- **Gerekçe**: US3. `OrderCanceledEvent` additive alan default'lu (eski tüketici kırılmaz, İlke I event
  kuralı). Giden imza R3'teki signer ile (mağaza private key).

## Açık dış bağımlılık (tek)

- **PG iyzico sandbox hazırlığı** (R7): DOĞRULANACAK. Kod tarafını bloke etmez (Order charge yolu zaten
  var); yalnız canlı tur (quickstart) için sandbox key + test kartı gerekir.