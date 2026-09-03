# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya >
Claude memory > Obsidian vault. Feature detayı BC haritasındaki `specs/*` yollarında.

**Mimari + kod konvansiyonları (taşınabilir katman): @docs/conventions.md** — DDD/VSA kuralları,
kod standartları, servisler-arası desenler orada. Bu dosya yalnız BU projeye özel bilgidir.

## Komutlar

Repo kökünden. Çözüm: `ECommerceWithAgentFramework.slnx` (`dotnet build/test` dosyayı
otomatik bulur, açıkça vermeye gerek yok). Format/lint script'i YOK.

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire)
dotnet test                                               # tüm testler
dotnet test tests/Basket.Api.Tests/Basket.Api.Tests.csproj          # tek proje
dotnet test --filter "FullyQualifiedName~BasketTests.AddItem"       # tek test
scripts/check-claude-spec-links.sh                        # BC haritası spec yolları guard'ı
scripts/check-flow-links.sh                               # FLOW.md domain-süreç anchor guard'ı (İLKE VII)
```

- **Sistemi hep Aspire AppHost'tan başlat**, tek servis değil — servisler birbirini/DB/RabbitMQ'yu
  service discovery + conn-string enjeksiyonuyla bulur; tek API bağımsız açılmaz.
- **Marten şeması otomatik kurulur** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **OpenAI kullanan servisler** (ChatAgent, ModerationAgent) açılışta fail-fast:
  `dotnet user-secrets set OpenAI:ApiKey <k> --project <proj>` (+ `OpenAI:Model`, ör. gpt-4o-mini).
- **Paket sürümleri yalnız `Directory.Packages.props`'ta** (Central Package Management); `.csproj`
  `PackageReference`'ı sürümsüz listeler. Sürüm ekle/değiştir → yalnız props.

## Teknoloji

.NET 10 (`Nullable`+`ImplicitUsings` açık) · **Marten** (Postgres = document/event store, Newtonsoft,
non-public setter+ctor) · **Wolverine** (in-proc bus `IMessageBus` + RabbitMQ fanout; handler assembly
taramasıyla) · **OpenIddict + ASP.NET Identity** (IdP) · **YARP** gateway · **MCP** (her API `/mcp`;
ChatAgent istemci) · **Microsoft Agent Framework** + `Microsoft.Extensions.AI` (ChatAgent,
ModerationAgent) · **Scrutor** (DI) · **xUnit + Shouldly** (saf domain birim testi).

## BC haritası

Her BC = kendi DB'si + şeması. Origin sütunu = BC'yi tanımlayan spec'in tam yolu (guard'lı); sonraki
feature'lar o feature'ın kendi spec'inde. Servisler `src/services/*`; destek `src/others`
(`Common`/`Shared`/`Identity.Server`), `src/aspire` (`AppHost`/`ServiceDefaults`), `src/agents`, `src/ui`.

| Servis | DB | Ne yapar | Origin spec |
|---|---|---|---|
| `catalog` | catalogDb | Zengin `Product`+`Category`+`Author`+`Publisher`+`ProductTag`+`SpecificationAttribute` (kitap künyesi: çok-yazar + tek yayınevi); admin düzenleme + yayın anahtarı + fiyat geçmişi (058, append-only `ProductPriceChange`) | `specs/040-catalog-domain-extract` |
| `basket` | basketDb | Kalıcı sepet + kalem; anonim sahiplik + login'de merge (057); stok tutmaz/süre yok (056), stok gerçeği checkout'ta | `specs/012-stock-reservation` |
| `order` | orderDb | Sipariş aggregate + yaşam döngüsü; orchestrator'dan broker Create/Confirm/Cancel; chat charge yolu; Confirm'de `OrderCompleted` fanout (Reviews + Storefront tüketir) | `specs/028-checkout-saga` |
| `checkout` | checkoutDb | Broker-only checkout sağası (`CheckoutProcess`, ayrı servis); CreateOrder→CommitStock→Charge→Confirm→ClearBasket; pivot=Charge, pivot-öncesi LIFO telafi + watchdog | `specs/049-checkout-orchestrator` |
| `payment` | paymentDb | Ödeme (mock; kart alanı yok, yalnız Amount; tek-faz Charge) | — |
| `stock` | stockDb | `ProductStock` (OnHand); ilk stok `ProductLinked`'ten; checkout düşümü broker'dan (056); admin artır/azalt + mutlak set (058) | `specs/014-supplier-stock-authority` |
| `storefront` | storefrontDb | Push-only read-model (`StorefrontView`); facet + varyant gruplama; filtre arama; kişisel feed (`UserPurchase` birikimi, ana sayfa) | `specs/003-storefront-read-model` |
| `customer` | customerDb | Wallet (tokenize kart, PAN yok) + AddressBook; izole, event yok | `specs/022-wallet-address-book` |
| `reviews` | reviewsDb | Satın-alma şartlı yorum; AI moderasyon AYRI worker'da (broker); özet event → Storefront | `specs/044-product-reviews` |
| `library` | libraryDb | Kullanıcı-ürün ilgi kayıtları; ilk dilim fiyat alarmı (yaşayan abonelik, email snapshot) + `NotificationRecord` izi; `ProductChangedEvent.OldPrice` tetiği → alarm başına `PriceAlarmTriggered` | `specs/060-price-alarm-mail` |
| `gateway` | — | YARP reverse proxy; tek giriş | — |
| `identity-server` | identityDb | OpenIddict + ASP.NET Identity; OIDC/OAuth + RBAC; dış agent için RFC 7591 DCR (`/connect/register`) + tek consent sayfası (Explicit) + revocation (061) | `specs/029-openiddict-migration` |
| `chat-agent` | — | AI asistan (MAF); MCP istemci + A2A ödeme (uzak PaymentGateway) | `specs/024-a2a-payment-agent` |
| `reviews-moderation-agent` | — | Reviews moderasyonu (DB'siz worker); `ReviewModerationRequested`→LLM→`ReviewModerated` | `specs/046-reviews-moderation-agent` |
| `notification-agent` | — | Fiyat alarmı maili (DB'siz worker); `PriceAlarmTriggered`→LLM compose→Mail.Mcp `send_mail`→`NotificationSent` | `specs/060-price-alarm-mail` |
| `mail-mcp` | — | İlk standalone MCP server; tek tool `send_mail` (MailKit→Mailpit); yalnız NotificationAgent tüketir, ChatAgent'a KAYITLI DEĞİL | `specs/060-price-alarm-mail` |

- **Ürün yazım yolu (050 pivot — first-party):** Çok-tedarikçi feed (Procurement + Supplier) SÖKÜLDÜ;
  mallar mağazanın. Düzenleme = 058 admin ekranları (künye/fiyat/stok/yayın); elle ürün OLUŞTURMA hâlâ yok
  (giriş 051 import). Catalog yeni üründe `ProductLinked` → Stock + `ProductChangedEvent` → Storefront.
  Silme yok (016); yayından kaldırma `IsDeleted:true` ile vitrini gizler (058).
- **WebApp agent-only (066):** Müşteri görsel ekranları (vitrin/ürün-liste/detay/kategori/sepet/checkout/
  hesap) SÖKÜLDÜ; kök (`/`) = mağaza asistanı chat (`Pages/MusteriHizmetleri.cshtml` route `"/"`). WebApp
  yalnız **admin** (ürün düzenleme + onboarding) + **login/OIDC** + **chat** + BFF proxy tutar. Talep edilen
  scope yalnız kimlik + yönetim (`catalog.write`/`stock.write`/`merchant.credentials.write`); müşteri
  alışveriş scope'ları kalktı. Eşleşmeyen route `MapFallback`→köke. Müşteri işlemleri agent/MCP yolunda
  (062–065 parite). TUZAK: `ICustomerRefitService` merchant-only KALDI (admin onboarding kullanır); adres/
  cüzdan yüzeyi silindi. AÇIK BULGU: anonim chat-sepet 4 katmanda bloke (bkz memory).
- **ModerationAgent (ayrı `reviews-moderation-agent` worker'ı):** Singleton ChatClientAgent (Temp=0,
  structured JSON, MCP'siz), retry→error queue. Moderasyon 046'da BC'den broker'lı worker'a taşındı
  (Reviews'te agent-framework yok; iletişim `ReviewModerationRequested`/`ReviewModerated` event'leriyle).
- **NotificationAgent (060):** TEK singleton `MailAgent` (workflow da compose/send ayrımı da YOK —
  kullanıcı kararları); tek LLM çağrısı maili yazar + `send_mail` tool'unu çağırır; her hata
  `NotificationException`→retry→error queue. Mailpit ham container (SMTP 1025/UI 8025); Mail.Mcp
  SMTP hedefini env'den alır (`SmtpOptions`).

## Projeye özel yetki + tuzaklar

- **RBAC (scope; İLKE V):** `AddAuthenticationAndAuthorizationExtension(config, ...scopes)`; `KnownScopes`
  kapalı registry, rol→scope map DB'de, admin `/Admin/*`'ten yönetir. Register → `customer` rolü.
  `[RequiredScope]` Wolverine mesaj handler'larına da uygulanır. Identity.Server **HTTPS zorunlu**.
- **TUZAK (`ScopeClaimArrayHandler`):** `context.TokenType` URN'dir (`TokenTypeIdentifiers.AccessToken`),
  hint DEĞİL — hint'le kıyaslarsan handler no-op → 403 → korumalı yüzeyde redirect döngüsü (tarihsel
  belirti: WebApp sepet ekranı; ekran 066'da söküldü ama tuzak scope-korumalı her uçta geçerli).
- **Dış agent MCP OAuth (061):** basket/order/customer/payment MCP'leri `RequireAuthorization` + RFC 9728
  keşif (`Common/Extensions/McpResourceMetadataExtension`); storefront/catalog/stock MCP anonim KALIR.
  UserKey (X-User-Key) yan yol — Bearer'la çakışmaz.
- **DCR istemcileri (061):** public+PKCE, `ConsentType=Explicit`, kapalı scope demeti
  `ExternalAgentDefaults` (yönetim scope'ları giremez, client_credentials verilmez); seed istemciler
  Implicit kalır (consent görmez). Redirect yalnız loopback + Claude callback (`DcrRequestValidator`).

## Yapma listesi

- **Çok-tedarikçi feed zincirini geri getirme:** Procurement + Supplier (+ eski Supplier.Gateway /
  IngestionAgent) 050'de SÖKÜLDÜ — model first-party, mallar mağazanın. Ürün girişi = ürün-CRUD.
- **`IConfiguration`'dan doğrudan okuma** (Options pattern istisnaları hariç).
- **MCP'yi agent-dışı koddan** imperatif çağırma.
- **Yeni saga için ayrı orchestration servisi** açma (god-service) — saga sürecin sahibi BC'de host edilir.
- **Çözüme (`.slnx`) dahil olmayan klasörlere dokunma** (staging/deneme kodu) — kapsam dışı.
