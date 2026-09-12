# Phase 0 Research: PG Aracılı Kart Saklama

Tüm NEEDS CLARIFICATION + entegrasyon bilinmeyenleri çözülür. Karar / Gerekçe / Elenen alternatif.
**Not:** iyzico'nun kendi mekaniği (Checkout Form, CardList/Delete, NON-3D) PG'nin içinde yaşar; bu
belge yalnız **mağaza↔PG** tarafını + PG'den beklenen sözleşmeyi tanımlar.

## R1 — Mimari sınır: iyzico PG'de, mağaza PG'yi tüketir

**Decision:** Mağaza (bu repo) iyzico SDK/HTTP import ETMEZ (FR-016). Tüm kart işleri PG'nin REST
sözleşmesi üzerinden. Değiştirilebilir seam PG'de (bugün iyzico, yarın kendi geliştirme/banka).

**Rationale:** Kullanıcı kararı ("PG iyzico'yu çağırır; mağaza PG ile konuşur"). İlke I: PG mevcut
sanksiyonlu dış entegrasyon (`PaymentGatewayClient`); izolasyon güçlenir. SC-006 denetlenebilir (grep
iyzico = 0).

**Alternatives considered:** Mağaza direkt iyzico — kullanıcı reddetti (PG komisyon/seam katmanı).

## R2 — Standalone hosted kart kaydı (US1)

**Decision:** "kart ekle" → mağaza PG'ye **add-session** çağırır → PG iyzico Checkout Form'u nominal
doğrulama tutarıyla + kart-saklama açık başlatır → PG hosted **link**'i mağazaya döner. Kullanıcı linki
tarayıcıda açar, kartı iyzico ekranında girer. PAN mağazaya/Claude'a uğramaz.

**Rationale:** iyzico'da ödemesiz saf hosted tokenizasyon yok; PAN toplama hosted Checkout Form ile
(tutar ister) → nominal doğrulama (sandbox'ta para hareketi yok). Bu mekanik PG'nin içinde; mağaza
yalnız "add-session → link" sözleşmesini görür.

**Alternatives considered:** PAN'ı mağazadan PG'ye POST (eski `GatewayCardTokenizer`) — PAN mağazadan
geçer, uyum ihlali, RED (bu yol sökülür).

## R3 — Callback ↔ kullanıcı korelasyonu + auth

**Decision:** `StartAddCard`, giriş yapmış `UserId`'yi tek-kullanımlık `AddCardSession` (Marten doc:
SessionId, UserId, oluşturma zamanı, durum) + PG'ye verilen `conversationId`'ye bağlar. Dönüş
(PG→mağaza callback ya da kullanıcının linkten döndüğü uç) **JWT beklemez**; `conversationId`/session
ile `UserId` çözülür, PG'den kullanıcı-handle alınıp Wallet'a yazılır. Uç tek-kullanımlık + süre-sınırlı.

**Rationale:** İlke V scope zorlaması kullanıcı yüzeyleri içindir; PG/iyzico callback'i makine-
çağrısıdır, tahmin-edilemez session + PG doğrulamasıyla güvenlenir.

**Alternatives considered:** Callback'i user JWT ile korumak — imkânsız (dış çağrı token taşımaz).

## R4 — Kart listeleme (canlı, PG'den)

**Decision:** `GetCardsForAgent`, Wallet'tan `PgUserHandle`'ı okur → PG **list-cards(userHandle)** →
`CardView { CardHandle(opak), Brand, Last4, Expiry, Alias, IsDefault(defaultCardHandle eşleşmesi) }`.
Yerel kart deposu yok. Handle yoksa boş liste. Mevcut `[Cached("cards",300)]` **KALDIRILIR** (canlı
doğruluk şart — FR-004/SC-003).

**Rationale:** FR-004/FR-005 + Clarify Q1 (opak handle döner, PAN/CVV asla). Cache canlı-doğruluğu bozar.

## R5 — Kart silme + varsayılan

**Decision:** Silme = PG **delete-card(userHandle, cardHandle)**; tarayıcı yok. Varsayılan =
`Wallet.SetDefaultCard(cardHandle)` yerelde `DefaultCardHandle` yazar (PG/iyzico'da varsayılan kavramı
yok — kullanıcı tercihi mağazada). Silinen kart varsayılansa temizlenir (FR-012). İlk kart otomatik
varsayılan (FR-001a) — CompleteAddCard sonrası DefaultCardHandle boşsa set edilir.

**Rationale:** cardHandle silme/varsayılan için gerekli opak referans; A-yolunda mağazada tutulan tek
kullanıcı-tercihi = varsayılan seçim (PAN yok).

## R6 — NON-3D çekim yolu

**Decision:** Çekim mevcut yol: checkout saga (049) pivot=Charge → PG `ChargeAsync`. `ChargeAsync`
payload'ı vaultToken yerine **PgUserHandle + CardHandle** taşır (+ price/buyer). PG içeride iyzico
NON-3D `payment` yapar. Order→Customer payment-context S2S bağlamı bu handle'ları taşır. Gerekirse
`ChargePaymentCommand`'a additive handle alanı (eski tüketici kırılmaz).

**Rationale:** İlke I — çekim Payment BC'nin işi; saga broker komut/yanıtı (049) korunur; yalnız kimlik
(token→handle) + mock→PG değişir. Yeni orchestration yok.

**RESOLVED (analyze I1):** İki çekim izi vardı — Order.Api `PlaceOrderForAgent` direkt `gateway.ChargeAsync`
vs checkout saga→mock Payment BC. **Karar: saga→PG.**
- Çekim sahibi = **Payment BC**: `PaymentEventHandlers.Handle(ChargePaymentCommand)` mock yerine PG NON-3D
  çağırır (handle'larla). `Payment` aggregate mock `ChargeRef` yerine PG paymentId/durum tutar.
- `PaymentGatewayClient` + payment-context client **Order.Api → Payment.Api** taşınır (çekim orada).
- `PlaceOrderForAgent` direkt çekimi KALDIRILIR → `confirmed` guard sonrası `StartCheckout` yayınlar.
- Seçilen kart: `StartCheckout`/`ChargePaymentCommand`'a additive `CardHandle` (null=varsayılan); Payment
  BC handle'ı + buyer'ı Customer payment-context S2S'ten çeker.

## R7 — Agent onayı (3DS yerine) — FR-014

**Decision:** Onay MCP tool Description'ında **kanonik** (070 deseni): "place_order öncesi kullanıcıya
tutar + kartın son 4 hanesini göster, açık onay al". Tool argümanı `confirmed:true` gerektirir (MCP
optional-default tuzağı: default `false`). Onaysız handler `Result.Error`, çekim başlamaz.

**Rationale:** NON-3D'de banka ekranı yok; onay agent konuşmasında. Prompt + kod guard birlikte.

## R8 — DropShop söküm sınırı (dar)

**Decision:** Sökülür: **yerel-vault PAN yolu** — `GatewayCardTokenizer` + `ICardTokenizer` (mağazadan
PG vault'una PAN POST eden akış) + yerel `SavedCard` kalıcılığı. **KALIR:** PG auth altyapısı
(`MerchantTokenProvider`, onboarding, `MerchantInformation`, `OnboardingGatewayTokenHandler`) — PG ile
konuşmak için hâlâ gerekli; `PaymentGatewayClient` (Order) çekim için kalır. Kart migrasyonu YOK
(sandbox/demo, yeniden eklenir).

**Rationale:** Kullanıcı "PG sökülmeyecek". Yalnız PAN-mağazadan-geçen eski akış uyum için gider; PG
altyapısı korunur.

## R9 — PG'den beklenen kart sözleşmesi (bağımlılık, kapsam-dışı impl)

**Decision:** PG'nin (harici repo) sunması gereken uçlar `contracts/pg-card-contract.md`'de tanımlanır:
add-session (→ hosted link + conversationId), complete/callback (→ userHandle), list-cards(userHandle)
(→ kart izdüşümleri), delete-card(userHandle, cardHandle), charge(userHandle, cardHandle, price, buyer)
NON-3D. 075 bu-repo tarafı bunları tüketir; PG-içi iyzico bunları karşılar (ayrı fasıl).

**Rationale:** Sözleşme AD'dır (bilinçli tekrar/izolasyon); mağaza PG-içi iyzico'yu bilmez.

## Çözülen NEEDS CLARIFICATION özeti

| # | Konu | Karar |
|---|------|-------|
| Q1 | Kart referansı | PG kart-handle opak döner (spec Clarifications) |
| Q2 | Ekleme zamanı | Standalone; PG hosted link, nominal (R2) |
| Q3 | İlk kart varsayılan | Evet (FR-001a, R5) |
| — | iyzico'yu kim çağırır | PG (R1, FR-016) |
| — | Kapsam | Bu repo (mağaza tarafı); PG-içi iyzico ayrı |
| R3 | Callback auth | conversationId + tek-kullanımlık session |
| R8 | DropShop | Yalnız yerel-vault PAN yolu söküm; PG altyapısı kalır |