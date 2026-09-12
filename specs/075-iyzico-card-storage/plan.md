# Implementation Plan: PG Aracılı Kart Saklama (A yolu — ince Wallet)

**Branch**: `075-iyzico-card-storage` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/075-iyzico-card-storage/spec.md`

## Summary

Kart saklamayı **PG aracılı** modele geçirir: mağaza (bu repo) PG ile REST konuşur, PG içeride iyzico
ile haberleşir. Kart verisi yalnız iyzico'nun barındırdığı ekranda (PG linkiyle açılır) toplanır; mağaza
PAN görmez. Wallet inceltilir: yerel `SavedCard` deposu sökülür, yalnız `UserId ↔ PG-kullanıcı-handle`
çapası + `defaultCardHandle` tutulur; kart listesi PG'den canlı çekilir. Çekim mevcut checkout saga
(049) üzerinden PG'ye NON-3D isteğiyle yapılır. **iyzico bu repoda YOK** — değiştirilebilir seam PG'de
("bugün iyzico, yarın kendi geliştirme/banka"). Mağaza yalnız PG'nin kart sözleşmesini bilir.

## Technical Context

**Language/Version**: .NET 10, C# (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (customerDb document store), Wolverine (in-proc bus + RabbitMQ broker
saga komut/yanıtı), MCP SDK. **iyzico SDK EKLENMEZ** (FR-016 — iyzico PG'de). PG ile HTTP/REST (mevcut
`PaymentGatewayClient` deseni).

**Storage**: customerDb (ince `Wallet` — PG kullanıcı-handle + defaultCardHandle). Kart verisi mağazada
DEĞİL, PG/iyzico'da.

**Testing**: xUnit + Shouldly. Domain-TDD (İlke VI): `Wallet` aggregate değişimleri (PG kullanıcı-handle
çapası, defaultCardHandle invariant'ı, `SavedCard` koleksiyon sökümü) test-first.

**Target Platform**: Linux/container (Aspire AppHost).

**Project Type**: Mikroservis (BC başına ayrı DB) — mevcut yapı.

**Performance Goals**: Kart listeleme canlı PG sorgusu; kullanıcı-görünür gecikme < ~2 sn (PG+iyzico
latency'ne bağlı). Çekim tek istek (senkron saga adımı).

**Constraints**: PAN/CVV mağaza sınırından içeri **hiç** girmez (uyum — pazarlıksız). Mağaza kodunda
iyzico'ya doğrudan bağımlılık YOK (FR-016). PG uçları Options pattern ile yapılandırılır (IConfiguration
doğrudan okuma yasak).

**Scale/Scope**: Demo/tek-tenant mağaza; kullanıcı başına birkaç kart. Çekim hacmi düşük.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **İlke I (BC izolasyonu):** ✅ PG = sanksiyonlu **dış entegrasyon** (mevcut `PaymentGatewayClient`
  deseni; DropShop harici repo). Mağaza BC'leri birbirinin DB'sine dokunmaz. Checkout saga → PG çekimi
  (049 pivot=Charge korunur). Order→Customer payment-context S2S REST kalır. iyzico bu repoda yok →
  izolasyon güçlenir.
- **İlke II (zengin aggregate):** ✅ `Wallet` zengin kalır (davranış: PG kullanıcı-handle çapası kur/oku,
  defaultCardHandle tek-varsayılan invariant'ı). `SavedCard` yerel koleksiyonu kalkar — kart artık
  aggregate state'i değil, PG/iyzico'nun.
- **İlke III (VSA/CQRS, repo yok):** ✅ Yeni agent slice'ları (`Features/Agents/`): add-session-başlat,
  callback-ile-kaydet, kart-listele (canlı, PG), kart-sil (PG), varsayılan-yap. MCP tool'ları ince
  sarmalayıcı. PG çağrısı handler'dan `IDocumentSession` + PG HTTP client ile.
- **İlke IV (Result):** ✅ Handler `Feature*ResultModel`, aggregate `ResultDomain`. PG hata kodları
  Customer resource sabitlerine eşlenir.
- **İlke V (scope yetki):** ✅ Kart tool'ları login-korumalı (customer scope). **Dikkat:** PG→mağaza
  callback ucu kullanıcı JWT'siyle korunamaz → conversationId/tek-kullanımlık session korelasyonu ile
  eşlenir (research R3).
- **İlke VI (Domain-TDD):** ✅ `Wallet` değişimleri test-first; tasks.md'de test task'ı önce.
- **İlke VII (FLOW.md):** ✅ `customer/FLOW.md` (kart yolu PG aracılı canlı) aynı PR'da güncellenir.
  Çekim adımı zaten checkout/FLOW.md + order'da; NON-3D notu eklenir.

**Sonuç:** İhlal yok. Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/075-iyzico-card-storage/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — PG-aracılı kart kararları
├── data-model.md        # Phase 1 — ince Wallet + PG izdüşümleri
├── quickstart.md        # Phase 1 — sandbox uçtan-uca doğrulama
├── contracts/           # Phase 1 — MCP tool + PG kart sözleşmesi + S2S + broker
└── tasks.md             # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/customer/Customer.Api/
├── Domains/Wallets/
│   ├── Wallet.cs                         # DEĞİŞİR: SavedCard koleksiyonu → PgUserHandle + DefaultCardHandle
│   ├── Entities/WalletEntities.cs        # SavedCard yerel entity SÖKÜLÜR (RemovedCard de)
│   ├── WalletMcpTools.cs                 # +add_card(oturum), +delete_card, +set_default_card
│   ├── WalletEndpointExtension.cs        # +PG callback ucu; payment-context ucu PG-handle döner
│   └── Features/Agents/
│       ├── StartAddCardForAgent.cs       # YENİ: PG'den hosted form linki al → döner
│       ├── CompleteAddCardForAgent.cs    # YENİ: callback → PG kullanıcı-handle → Wallet.SetPgUserHandle
│       ├── GetCardsForAgent.cs           # DEĞİŞİR: yerel değil PG'den canlı; +cardHandle; Cache KALKAR
│       ├── DeleteCardForAgent.cs         # YENİ: PG delete (userHandle+cardHandle)
│       ├── SetDefaultCardForAgent.cs     # YENİ: defaultCardHandle yaz
│       └── GetPaymentContextForAgent.cs  # DEĞİŞİR: vaultToken → PgUserHandle + CardHandle
├── Infrastructure/PaymentGateway/        # YENİ/GENİŞLER: PG kart-sözleşmesi client'ı
│   ├── IPgCardClient.cs                   # port: StartAddSession/CompleteAdd/ListCards/DeleteCard
│   ├── PgCardClient.cs                    # PG REST impl (mevcut merchant-key/token deseni)
│   └── Options/PgCardOptions.cs           # PG kart uçları BaseUrl (Options pattern)
└── Infrastructure/Tokenization/          # SÖKÜLÜR: ICardTokenizer + GatewayCardTokenizer (yerel-vault PAN yolu)
    └── (yerel vault)                     # (MerchantTokenProvider/Onboarding KALIR — PG auth hâlâ gerekli)

src/services/payment/Payment.Api/
├── Domains/Payments/Payment.cs           # DEĞİŞİR: mock ChargeRef → PG paymentId/durum
├── PaymentEventHandlers.cs               # DEĞİŞİR: mock Charge → PG NON-3D (handle'larla)
├── Http/CustomerPaymentContextClient.cs  # YENİ (Order.Api'den TAŞINIR): PgUserHandle+CardHandle+buyer S2S
└── Http/PaymentGatewayClient.cs          # YENİ (Order.Api'den TAŞINIR): ChargeAsync NON-3D (handle'larla)

src/services/order/Order.Api/
├── Http/PaymentGatewayClient.cs          # SÖKÜLÜR (direkt çekim Order'dan kalkar → Payment BC'ye taşındı)
├── Http/CustomerPaymentContextClient.cs  # SÖKÜLÜR (Payment.Api'ye taşındı)
└── Domains/Orders/Features/Agents/PlaceOrderForAgent.cs  # DEĞİŞİR: direkt gateway.ChargeAsync KALKAR; confirmed guard sonrası StartCheckout yayınlar

src/others/Shared/CheckoutMessages.cs     # StartCheckout + ChargePaymentCommand: +CardHandle (additive; null=varsayılan)
```

**Structure Decision:** Mağaza PG'nin kart sözleşmesini tüketir; iyzico bu repoda YOK (FR-016 —
seam PG'de). DropShop'a özgü **yerel-vault PAN yolu** (`GatewayCardTokenizer`) sökülür; yerine PG-hosted
form + canlı liste. PG **auth altyapısı** (merchant-key, `MerchantTokenProvider`, onboarding) KALIR.
Yeni saga/orchestration servisi AÇILMAZ. **Çekim yolu = saga→PG (analyze I1):** canonical = checkout
saga (049) → `ChargePaymentCommand` → **Payment BC** → PG NON-3D. Order.Api direkt-çekim yolu
(`PlaceOrderForAgent`→`PaymentGatewayClient`) SÖKÜLÜR; `place_order` onay sonrası `StartCheckout`
yayınlar. `PaymentGatewayClient` + payment-context client Order→Payment.Api'ye taşınır (çekim Payment
BC'nin işi — İlke I).

**Bağımlılık (kapsam-dışı):** PG'nin (harici repo) yeni kart uçları — hosted-form-session + list-cards +
delete-card — 075'in bu-repo tarafı bunları **tüketir**; PG-içi iyzico implementasyonu ayrı fasıl.

## Complexity Tracking

> Anayasa ihlali yok — bu tablo boş.