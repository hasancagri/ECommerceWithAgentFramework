# Implementation Plan: UCP Checkout Kanalı

**Branch**: `072-ucp-checkout-channel` | **Date**: 2026-09-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/072-ucp-checkout-channel/spec.md`

## Summary

Mağazaya dış AI platformları/agent'ları için **UCP (Universal Commerce Protocol)** uyumlu bir
checkout kanalı ekleriz. Yeni izole `ucp` Bounded Context'i: `.well-known/ucp` keşif profili +
`.well-known/oauth-authorization-server` + checkout session uçları (create/update/complete/cancel)
+ fulfillment ve discount uzantıları + imzalı giden webhook. Session tamamlanınca sipariş, mevcut
sipariş borusuna **already-captured** olarak devredilir (sanksiyonlu gRPC → Order); ödeme mağazanın
PaymentGateway'i üzerinden **iyzico sandbox**'ta tahsil edilir. İç web/saga checkout dokunulmaz.
Kimlik OAuth-native (scope `dev.ucp.shopping.checkout`); istek bütünlüğü RFC 9421 imzasıyla
doğrulanır, zorlama opsiyonel bayrakla. Uçtan uca kanıt için platform rolünü canlandıran `ucp-sim`
simülatörü Claude Desktop'tan sürülür.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres document/event store), Wolverine (in-proc bus + RabbitMQ
fanout), OpenIddict (OAuth scope + DCR — mevcut Identity.Server), gRPC (Order'a external-order),
System.Security.Cryptography (ES256/P-256, Content-Digest SHA-256) + NSec.Cryptography (Ed25519) —
RFC 9421 elle minimal uygulama. HTTP client → PaymentGateway (Order içinden mevcut).

**Storage**: Yeni `ucpDb` (Postgres + Marten şeması). Session durumu + katalog projeksiyonu +
giden webhook teslim izi. Başka BC'nin DB'sine erişim yok (İlke I).

**Testing**: xUnit + Shouldly. Saf domain (session aggregate durum makinesi + VO'lar + imza
doğrulayıcı saf birimleri) **test-first** (İlke VI). Handler/endpoint/webhook/gRPC test-sonra +
`ucp-sim` ile canlı doğrulama (quickstart).

**Target Platform**: Aspire AppHost üzerinde Linux/container servis; YARP gateway arkasında.

**Project Type**: Web service (mikroservis BC) + yardımcı simülatör agent'ı.

**Performance Goals**: Etkileşimli agent akışı; session işlemleri kullanıcı-algısı "anında"
(sub-saniye). Ölçek: demo/sandbox (tek haneli eşzamanlı platform oturumu).

**Constraints**: PaymentGateway repo'suna dokunulmaz (dış depo). Ödeme iyzico **sandbox** (gerçek
para yok, 3DS-siz test kartı). İç web checkout regresyonu yok. UCP spec sürümü iş başındaki
`main`'e sabit. Para birimi TRY.

**Scale/Scope**: Tek yeni BC + 1 simülatör + Shared'a additive kontrat (proto + event) + Order'a
external-order girişi + Identity/AppHost/Gateway wiring.

## Constitution Check

*GATE: Phase 0'dan önce geçmeli; Phase 1 sonrası yeniden bakılır.*

- **İlke I — BC İzolasyonu**: ✅ Yeni `ucp` BC kendi `ucpDb`'si + kendi modeli. Context-arası tek
  kanal: (a) katalog projeksiyonu ürün/stok **integration event**'leriyle beslenir; (b) sipariş
  oluşturma **sanksiyonlu gRPC** ile Order'a (hedefli komut, saga-komşusu; sunucu ince sarmalayıcı,
  `IMessageBus`'a devreder); (c) sipariş olayları fanout. UCP'nin kendi `Line Item`/`Catalog Item`
  modeli Catalog'un zengin `Product`'ından ayrı (aynı kavram farklı model). PG çağrısı **Order
  içinden** yapılır (mevcut chat charge yolu) — UCP BC PG'ye doğrudan dokunmaz.
- **İlke II — Zengin Aggregate**: ✅ `UcpCheckoutSession` aggregate, durum makinesi + toplam/indirim/
  kargo invariant'ları içeride; koleksiyonlar private, mutasyon yalnız davranış metotlarından. VO'lar
  `record` + private ctor + `Create`.
- **İlke III — VSA + CQRS, Repository yok**: ✅ Feature başına static class; `Features/Commands`
  (CreateSession/UpdateSession/ApplyDiscount/SelectFulfillment/CompleteSession/CancelSession),
  `Features/Queries` (GetSession). Handler doğrudan `IDocumentSession`; slice-arası `IMessageBus`.
  Endpoint'ler Minimal API + `*EndpointExtension`. **Not:** UCP uçları dış-protokol REST yüzeyidir
  (iç müşteri MCP-only kuralının kapsamı değil — admin/S2S gibi meşru REST); MCP tool DEĞİL.
- **İlke IV — Result Pattern**: ✅ Handler'lar `FeatureObjectResultModel<T>` vb.; aggregate
  `ResultDomain`. Aggregate private ctor nedeniyle yanıt için ince `SessionResult` zarfı (071 dersi).
- **İlke V — Scope Yetki**: ✅ Yeni scope `dev.ucp.shopping.checkout` → `KnownScopes` (kod-sahipli
  kapalı registry). Uçlar `.RequireAuthorization`. Platform = **makine kimliği** (`client_credentials`
  + statik scope, RBAC dışı — İlke V makine kimliği kuralı; 071'in `acp-gateway` m2m emsali); UCP
  siparişinin kullanıcısı sentetik. **ACP'nin API-key sapması ORTADAN KALKAR** (scope-native).
  Per-user `authorization_code` linking gelecekte (research'te not).
- **İlke VI — Domain-TDD**: ✅ Session durum geçişleri, toplam/indirim/kargo hesap kuralları, imza
  doğrulayıcı saf birimleri test-first; `tasks.md`'de test task'ları implementasyondan önce.
- **İlke VII — FLOW.md**: ✅ `src/services/ucp/FLOW.md` + her `.csproj`'a linked-file + guard
  (`check-flow-links.sh`) kapsamı.
- **Teknoloji kısıtları**: ✅ .NET 10, Marten, Wolverine, Scrutor DI marker'ları, Options pattern
  (`AcpPlatformOption` yerine `UcpPlatformOption`/`UcpSigningOption`), tek GlobalUsings, Aspire
  AppHost'tan çalıştırma. Agent (`ucp-sim`) Singleton.

**Sonuç**: Gate GEÇTİ. Gerekçelendirilmiş sapma YOK (ACP'deki API-key sapması UCP'de kapanır).
Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/072-ucp-checkout-channel/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1
│   ├── ucp-checkout-endpoints.md
│   ├── ucp-discovery-profile.md
│   ├── ucp-webhook.md
│   └── external-order-grpc.md
└── tasks.md             # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/ucp/
├── FLOW.md                                  # İlke VII domain süreç belgesi
└── Ucp.Api/
    ├── Ucp.Api.csproj                       # linked <None Include="..\FLOW.md">
    ├── GlobalUsings.cs
    ├── Program.cs                           # Marten(ucpDb), Wolverine, OAuth, MapMcp YOK; REST + .well-known
    ├── Options/
    │   ├── UcpPlatformOption.cs             # platform kimliği + webhook hedefi
    │   └── UcpSigningOption.cs              # imza anahtarları + RequireSignatures bayrağı
    ├── Domains/Sessions/
    │   ├── UcpCheckoutSession.cs            # zengin aggregate + durum makinesi
    │   ├── ValueObjects/UcpSessionValueObjects.cs   # LineItem, Totals, Buyer, Fulfillment, Discount VO'ları
    │   ├── UcpCheckoutSessionEndpointExtension.cs
    │   └── Features/
    │       ├── Commands/{CreateSession,UpdateSession,ApplyDiscount,SelectFulfillment,CompleteSession,CancelSession}.cs
    │       ├── Queries/GetSession.cs
    │       └── SessionResult.cs             # aggregate→yanıt ince zarf
    ├── Discovery/
    │   ├── WellKnownEndpointExtension.cs    # /.well-known/ucp + /.well-known/oauth-authorization-server
    │   └── UcpProfile.cs                    # capabilities + payment handler + JWKS keys
    ├── CatalogProjection/
    │   ├── UcpCatalogItem.cs                # satılabilir ürün anlık görüntü (id/başlık/fiyat/uygunluk)
    │   ├── UcpCatalogEndpointExtension.cs   # catalog_lookup + catalog_search
    │   └── UcpCatalogEventHandlers.cs       # ProductChanged/StockChanged tüketimi
    ├── Signatures/
    │   ├── HttpMessageSignatureVerifier.cs  # RFC 9421 gelen doğrulama (ES256/Ed25519) + RFC 9530
    │   └── HttpMessageSigner.cs             # RFC 9421 giden webhook imzalama
    ├── Grpc/
    │   └── ExternalOrderClient.cs           # Order'a CreateExternalOrder çağrısı (already-captured)
    ├── Webhooks/
    │   ├── UcpWebhookSender.cs              # imzalı gönderim + retry/backoff
    │   ├── OrderEventsHandler.cs            # OrderCompleted/OrderCanceled tüketip webhook tetikle
    │   └── OutboundDelivery.cs              # teslim izi
    ├── Constants/UcpResourceConstants.cs
    └── Dependencies/DependencyExtensions.cs

src/agents/Ucp.Sim/                          # platform SİMÜLATÖRÜ (test aracı, ChatGPT arka-ucu rolü)
├── Ucp.Sim.csproj
├── Program.cs
├── UcpSimTools.cs                           # MCP tool'ları mağazanın /ucp cephesini çağırır
├── WebhookInbox.cs                          # POST /inbox webhook alıcısı
└── Options/UcpSimOptions.cs

tests/Ucp.Api.Tests/
├── UcpCheckoutSessionTests.cs               # durum makinesi + toplam/indirim/kargo (test-first)
└── HttpMessageSignatureTests.cs             # imza doğrula/imzala saf birimleri (test-first)

# Shared (additive kontratlar)
src/others/Shared/Protos/external_order.proto        # CreateExternalOrder (071'de vardı, yeniden)
src/others/Shared/IntegrationEvents.cs               # OrderCanceledEvent (additive, default'lu)
src/others/Shared/RabbitMqConstants.cs               # ucp.events kuyruğu
src/others/Shared/UcpSigningKeys.cs                  # imza anahtar sözleşmesi (mint↔verify iki taraf)

# Order (external-order girişi — mevcut chat charge yolunu yeniden kullanır)
src/services/order/Order.Api/Domains/Orders/Features/Agents/CreateExternalOrder.cs
src/services/order/Order.Api/Grpc/ExternalOrderGrpcService.cs   # ince sarmalayıcı → IMessageBus

# Wiring
src/aspire/AppHost/AppHost.cs                # ucp-api + ucp-sim kayıt
src/others/Identity.Server/Config.cs         # ucp-platform m2m istemci + dev.ucp.shopping.checkout scope
src/services/gateway/Gateway/appsettings*    # /ucp/{**} + /.well-known rotaları
ECommerceWithAgentFramework.slnx             # 2 yeni proje
```

**Structure Decision**: 071'in (silinen) yapısına biçim olarak benzer ama **temiz yazım** —
`ucp` adı, UCP session/keşif/uzantı modeli, gerçek RFC 9421 imzası (stub SPT DEĞİL) ve ödeme
Order-içi PG charge (AlreadyCaptured). Fiziksel klasörler solution klasörleriyle birebir.

## Complexity Tracking

Constitution Check sapmasız geçti → boş.