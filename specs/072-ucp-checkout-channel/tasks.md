---
description: "Task list for UCP Checkout Kanalı"
---

# Tasks: UCP Checkout Kanalı

**Input**: Design documents from `/specs/072-ucp-checkout-channel/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: İlke VI (Domain-TDD) — saf domain (session aggregate durum makinesi, VO'lar, imza
saf birimleri) için test task'ı ZORUNLU ve implementasyondan ÖNCE. Handler/endpoint/webhook/gRPC/
wiring test-sonra + `ucp-sim` canlı doğrulama.

**Organization**: User story bazlı fazlar. MVP = US1.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 `src/services/ucp/Ucp.Api/Ucp.Api.csproj` oluştur (net10; ref: Common, Shared, ServiceDefaults; sürümsüz PackageReference)
- [X] T002 `Ucp.Api` + `Ucp.Sim`'i `ECommerceWithAgentFramework.slnx`'e ekle
- [X] T003 [P] `src/services/ucp/Ucp.Api/GlobalUsings.cs`
- [X] T004 [P] `src/services/ucp/Ucp.Api/Properties/launchSettings.json` (Production default tuzağı — bkz memory: aspire-service-needs-launchsettings)
- [X] T005 [P] `src/services/ucp/Ucp.Api/Constants/UcpResourceConstants.cs` (hata kodu sabitleri)
- [X] T006 [P] `src/services/ucp/Ucp.Api/Options/UcpPlatformOption.cs` + `UcpSigningOption.cs` (Options pattern; BindConfiguration + ValidateOnStart) + `appsettings*.json`
- [X] T007 `src/services/ucp/Ucp.Api/Program.cs` iskelet: Marten(`ucpDb`, ApplyAllDatabaseChangesOnStartup) + Wolverine + ServiceDefaults + `AddAllDependencies()` + auth extension (scope)
- [X] T008 [P] `Directory.Packages.props`'a `NSec.Cryptography` sürümü ekle (Ed25519; Central Package Management)
- [X] T009 `src/aspire/AppHost/AppHost.cs`: `ucp-api` + `ucpDb` (Postgres) kaydı; `AppHost.csproj` referansı
- [X] T010 `src/agents/Ucp.Sim` iskelet: `Ucp.Sim.csproj` + `Program.cs` + `Options/UcpSimOptions.cs` + `GlobalUsings.cs` + `launchSettings.json`; AppHost'a `ucp-sim` kaydı
- [X] T011 `src/services/ucp/FLOW.md` iskelet (domain süreç, doldurma Polish'te) + `Ucp.Api.csproj`'a linked-file `<None Include="..\FLOW.md" Link="FLOW.md" />`

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ US1 dahil tüm story'ler bundan önce ilerleyemez** (Shared kontrat + Order girişi + kimlik + gateway).

- [X] T012 `src/others/Shared/Protos/external_order.proto` — `CreateExternalOrder` servisi (external_ref, currency, amount_minor, buyer, line_items, ucp_payment_ref → order_ref, charged, message)
- [X] T013 [P] `src/others/Shared/IntegrationEvents.cs` — `OrderCanceledEvent` (additive, default'lu alanlar; eski tüketici kırılmaz)
- [X] T014 [P] `src/others/Shared/RabbitMqConstants.cs` — `ucp.events` kuyruk/exchange sabitleri
- [X] T015 [P] `src/others/Shared/UcpSigningKeys.cs` — imza anahtar sözleşmesi (signer↔verifier iki-taraf)
- [X] T016 `src/services/order/Order.Api/Domains/Orders/Features/Agents/CreateExternalOrder.cs` — sentetik UCP kullanıcısı adına sipariş; PG charge (mevcut `PlaceOrderForAgent → PaymentGatewayClient.ChargeAsync` deseni; iyzico sandbox); başarısız→charged=false; başarılı→`StartCheckout(AlreadyCaptured)`; external_ref→deterministik OrderId (idempotent)
- [X] T017 `src/services/order/Order.Api/Grpc/ExternalOrderGrpcService.cs` — ince sarmalayıcı → `IMessageBus` (iş mantığı yok); `Order.Api/Program.cs`'e kayıt; proto'yu `Order.Api.csproj`'a (`global::Grpc.Core` çakışma dersi)
- [X] T018 `src/others/Identity.Server/Config.cs` — `ucp-platform` `client_credentials` istemcisi + `dev.ucp.shopping.checkout` scope'unu `KnownScopes`'a ekle
- [X] T019 `src/services/gateway/Gateway/appsettings*.json` — `/ucp/{**}` + `/.well-known/ucp` + `/.well-known/oauth-authorization-server` rotaları

**Checkpoint**: Sipariş girişi + kimlik + gateway hazır — US1 başlayabilir.

---

## Phase 3: User Story 1 - Dış platform UCP ile satın alma tamamlar (Priority: P1) 🎯 MVP

**Goal**: create→update→(fulfillment+discount)→complete; PG/iyzico sandbox charge; already-captured sipariş.

**Independent Test**: `ucp-sim`'den session aç→kalem+adres+kargo+kod→complete → tek ödemeli sipariş + PG tahsilat; tekrar complete → yeni sipariş yok.

### Tests (Domain-TDD, ÖNCE — FAIL etmeli) ⚠️

- [X] T020 [P] [US1] `tests/Ucp.Api.Tests/UcpSessionValueObjectsTests.cs` — LineItem/Totals/Buyer/Fulfillment/AppliedDiscount `Create` + guard'lar (test-first)
- [X] T021 [P] [US1] `tests/Ucp.Api.Tests/UcpCheckoutSessionTests.cs` — durum makinesi + invariant'lar: create(+6h), ReplaceLineItems(tam değişim, boş red), SelectFulfillment→totals shipping, ApplyDiscountCodes→totals discount + geçersiz kod messages, MarkReadyIfComplete, BeginComplete(ready-değil red/süre-dolu red/idempotent), MarkCompleted/Cancel (test-first)

### Implementation

- [X] T022 [P] [US1] `src/services/ucp/Ucp.Api/Domains/Sessions/ValueObjects/UcpSessionValueObjects.cs` (record + private ctor + Create)
- [X] T023 [US1] `src/services/ucp/Ucp.Api/Domains/Sessions/UcpCheckoutSession.cs` — zengin aggregate + `UcpSessionStatus` enum + davranış metotları (her metot `/// <summary>` ne+neden — bkz [[doc-comment-ucp-methods]])
- [X] T024 [US1] `src/services/ucp/Ucp.Api/Domains/Sessions/Features/SessionResult.cs` — aggregate→yanıt ince zarf (private ctor dersi)
- [X] T025 [P] [US1] `Features/Commands/CreateSession.cs`
- [X] T026 [P] [US1] `Features/Commands/UpdateSession.cs` (line_items tam değişim + buyer)
- [X] T027 [P] [US1] `Features/Commands/SelectFulfillment.cs` (kargo → totals)
- [X] T028 [P] [US1] `Features/Commands/ApplyDiscount.cs` (kod → totals; geçersiz messages)
- [X] T029 [P] [US1] `Features/Queries/GetSession.cs`
- [X] T030 [P] [US1] `Features/Commands/CancelSession.cs`
- [X] T031 [US1] `src/services/ucp/Ucp.Api/Grpc/ExternalOrderClient.cs` — Order'a `CreateExternalOrder` çağrısı (contracts/external-order-grpc.md)
- [X] T032 [US1] `Features/Commands/CompleteSession.cs` — ready+süre guard, idempotency, totals tazele, gRPC→AlreadyCaptured, order_ref; ödeme başarısız→completed olmaz+messages
- [X] T033 [US1] `Domains/Sessions/UcpCheckoutSessionEndpointExtension.cs` — Minimal API map (create/update/get/complete/cancel) + `.RequireAuthorization` scope; `Program.cs`'e map
- [X] T034 [US1] `src/agents/Ucp.Sim/UcpSimTools.cs` — token(client_credentials) + create/update/complete/cancel tool'ları; Claude Desktop MCP kaydı (quickstart)

**Checkpoint**: US1 uçtan uca satın alma çalışır (imza/keşif/webhook olmadan). **MVP burada.**

---

## Phase 4: User Story 2 - Keşif + katalog (Priority: P2)

**Goal**: `.well-known/ucp` profil + oauth metadata + catalog lookup/search.

**Independent Test**: profil çek→capabilities/handlers/keys; `/ucp/catalog?q=` → ≥1 satılabilir ürün.

- [X] T035 [P] [US2] `CatalogProjection/UcpCatalogItem.cs` (read-model)
- [X] T036 [US2] `CatalogProjection/UcpCatalogEventHandlers.cs` — `ProductChangedEvent` + stok olayı tüketimi (`ucp.events` Sequential; Storefront push-only deseni)
- [X] T037 [US2] `CatalogProjection/UcpCatalogEndpointExtension.cs` — `GET /ucp/catalog/{id}` + `GET /ucp/catalog?q=`
- [X] T038 [P] [US2] `Discovery/UcpProfile.cs` — capabilities(checkout)+extensions(fulfillment,discount)+payment_handlers+JWKS (options'tan)
- [X] T039 [US2] `Discovery/WellKnownEndpointExtension.cs` — `/.well-known/ucp` + `/.well-known/oauth-authorization-server` (OpenIddict metadata'sına yönlendir)
- [X] T040 [US2] `Ucp.Sim`: keşif + katalog arama tool'ları

**Checkpoint**: US1 + US2 bağımsız çalışır.

---

## Phase 5: User Story 4 - Dış agent yetki + istek bütünlüğü (Priority: P2)

**Goal**: scope zorlaması + gelen RFC 9421 imza doğrulama (opsiyonel bayrak).

**Independent Test**: scope'suz→reddedilir; RequireSignatures=on iken imzasız/bozuk→401; off iken geçer.

### Tests (Domain-TDD, ÖNCE — FAIL etmeli) ⚠️

- [X] T041 [P] [US4] `tests/Ucp.Api.Tests/HttpMessageSignatureTests.cs` — SignatureBaseBuilder (RFC 9421 §2 bileşen sırası) + Content-Digest (RFC 9530 SHA-256) + verifier: geçerli/bozuk/eksik, ES256 + Ed25519, `created` tazelik (test-first)

### Implementation

- [X] T042 [P] [US4] `Signatures/SignatureBaseBuilder.cs` + `ContentDigest.cs` (saf; RFC 9421/9530)
- [X] T043 [US4] `Signatures/HttpMessageSignatureVerifier.cs` — ES256 (BCL `ECDsa`) + Ed25519 (NSec); public key profil JWKS'inden `kid` ile; tazelik/replay guard
- [X] T044 [US4] Gelen istek doğrulama middleware/filter — `/ucp` checkout uçlarında; `UcpSigningOption.RequireSignatures` bayrağıyla (on=401, off=varsa doğrula/yoksa geç)
- [X] T045 [US4] `Ucp.Sim`: giden istekleri imzala (RequireSignatures=on yolunu test edebilmek için)

**Checkpoint**: Kanal yetki + bütünlük doğrulaması çalışır.

---

## Phase 6: User Story 3 - Sipariş olay bildirimi (Priority: P3)

**Goal**: OrderCompleted/OrderCanceled → platforma imzalı webhook + retry.

**Independent Test**: sipariş onay/iptal → `ucp-sim` `/inbox` imzalı olay alır + imza doğrular; ilk teslim başarısız→retry.

### Tests (Domain-TDD, ÖNCE — FAIL etmeli) ⚠️

- [X] T046 [P] [US3] `tests/Ucp.Api.Tests/HttpMessageSignerTests.cs` — giden imza header'ları (`Signature`/`Signature-Input`/`Content-Digest`) doğru üretilir; SignatureBaseBuilder'ı yeniden kullanır (test-first)

### Implementation

- [X] T047 [US3] `Signatures/HttpMessageSigner.cs` — mağaza private key ile imzalar (US4'teki SignatureBaseBuilder'a bağlı)
- [X] T048 [P] [US3] `Webhooks/OutboundDelivery.cs` — teslim izi (attempts/delivered/lastError)
- [X] T049 [US3] `Webhooks/UcpWebhookSender.cs` — imzalı POST + retry/backoff (≤3-4)
- [X] T050 [US3] `Webhooks/OrderEventsHandler.cs` — `OrderCompleted` + `OrderCanceledEvent` tüketip sender'ı tetikle (`order.confirmed`/`order.canceled`)
- [X] T051 [US3] `Ucp.Sim/WebhookInbox.cs` — `POST /inbox` alıcı + gelen imzayı doğrula

**Checkpoint**: Tüm story'ler bağımsız çalışır.

---

## Phase 7: Polish & Cross-Cutting

- [X] T052 [P] `src/services/ucp/FLOW.md` doldur (domain süreç, kenar-anchor) + `scripts/check-flow-links.sh` geçir
- [X] T053 [P] `CLAUDE.md` BC haritasına `ucp` satırı ekle + `scripts/check-claude-spec-links.sh` geçir
- [X] T054 `dotnet test` — tüm domain birim testleri yeşil (İlke VI kapsamı)
- [ ] T055 `quickstart.md` senaryolarını Claude Desktop'tan koş (canlı tur: keşif→satın alma→webhook→imza zorlaması→regresyon) — MANUEL; ön-koşul: Aspire stack ayakta + PG iyzico sandbox key + `UcpOrder:MerchantId` onboard merchant'a set (R7 DOĞRULANACAK) + `ucp-sim` Claude Desktop MCP kaydı

---

## Dependencies & Execution Order

- **Setup (P1)**: bağımsız, hemen.
- **Foundational (P2)**: Setup'a bağlı; **tüm story'leri BLOKLAR**.
- **US1 (P3)**: Foundational'a bağlı. MVP.
- **US2 (P4)**: Foundational'a bağlı; US1'den bağımsız.
- **US4 (P5)**: Foundational'a bağlı; US1 uçlarının üstüne imza katmanı ekler (uçlar US1'de map'lenmiş olur).
- **US3 (P6)**: Foundational + **US4** (SignatureBaseBuilder'ı yeniden kullanır).
- **Polish (P7)**: istenen story'ler bitince.

### Story içi

- Domain-TDD: T020/T021 (US1), T041 (US4), T046 (US3) implementasyondan ÖNCE, FAIL etmeli.
- VO → aggregate → command/handler → endpoint. Model → service → endpoint.

### Parallel fırsatları

- Setup [P]: T003,T004,T005,T006,T008.
- Foundational [P]: T013,T014,T015 (Shared, ayrı dosyalar).
- US1 [P] testler T020,T021; [P] command'lar T025-T030 (ayrı dosyalar, aggregate T023 sonrası).
- US2 [P]: T035,T038.

---

## Parallel Example: User Story 1

```bash
# Testler (önce, FAIL):
Task: "T020 UcpSessionValueObjectsTests"
Task: "T021 UcpCheckoutSessionTests"
# Aggregate + VO sonrası command'lar birlikte:
Task: "T025 CreateSession" ; "T026 UpdateSession" ; "T027 SelectFulfillment"
Task: "T028 ApplyDiscount" ; "T029 GetSession" ; "T030 CancelSession"
```

---

## Implementation Strategy

- **MVP**: Phase 1 → 2 → 3 (US1). DUR + doğrula (satın alma uçtan uca). İmza/keşif/webhook olmadan çalışır.
- **Artımlı**: +US2 (keşif) → +US4 (yetki/imza) → +US3 (webhook). Her biri bağımsız test + demo.
- **Not**: Ödeme Order içinde (PG/iyzico sandbox); UCP BC PG'ye dokunmaz. PG sandbox key ön-koşulu canlı tur (T055) için (R7 DOĞRULANACAK).