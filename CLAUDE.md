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
| `catalog` | catalogDb | Zengin `Product`+`Category`+`Author`+`Publisher`+`ProductTag`+`SpecificationAttribute` (kitap künyesi: çok-yazar + tek yayınevi) | `specs/040-catalog-domain-extract` |
| `basket` | basketDb | Sepet + kalem; Stock'a gRPC rezervasyon (fail-closed) | `specs/012-stock-reservation` |
| `order` | orderDb | Sipariş + `CheckoutSaga` (durable, pivot-kurallı); satın-alma kanıtı gRPC | `specs/028-checkout-saga` |
| `payment` | paymentDb | Ödeme (mock; kart alanı yok, yalnız Amount) | — |
| `stock` | stockDb | `ProductStock` (OnHand); ilk stok `ProductLinked`'ten; gRPC rezervasyon sunucu | `specs/014-supplier-stock-authority` |
| `storefront` | storefrontDb | Push-only read-model (`StorefrontView`); facet + varyant gruplama; filtre arama | `specs/003-storefront-read-model` |
| `customer` | customerDb | Wallet (tokenize kart, PAN yok) + AddressBook; izole, event yok | `specs/022-wallet-address-book` |
| `reviews` | reviewsDb | Satın-alma şartlı yorum; AI moderasyon AYRI worker'da (broker); özet event → Storefront | `specs/044-product-reviews` |
| `reco-trainer` | recoTrainerDb | **Python** kişiselleştirme beyni (Aspire `AddUvicornApp`); `Signal` feature store + **precompute** zevk profili (APScheduler job → sklearn `TfidfVectorizer`+`KMeans` → `taste_profile`; fitted model saf-dosya joblib); gezinme HTTP + satın-alma Storefront `PurchaseEnriched`; serving OKUR; ranking Storefront'ta; ML eğitimi faz-2 | `specs/053-personalized-home-feed` |
| `gateway` | — | YARP reverse proxy; tek giriş | — |
| `identity-server` | identityDb | OpenIddict + ASP.NET Identity; OIDC/OAuth + RBAC | `specs/029-openiddict-migration` |
| `chat-agent` | — | AI asistan (MAF); MCP istemci + A2A ödeme (uzak PaymentGateway) | `specs/024-a2a-payment-agent` |
| `reviews-moderation-agent` | — | Reviews moderasyonu (DB'siz worker); `ReviewModerationRequested`→LLM→`ReviewModerated` | `specs/046-reviews-moderation-agent` |

- **Ürün yazım yolu (050 pivot — first-party):** Çok-tedarikçi feed (Procurement + Supplier) SÖKÜLDÜ;
  mallar mağazanın. Ürün girişi = ürün-CRUD (SONRAKİ feature). Catalog yeni üründe `ProductLinked` → Stock
  (ilk OnHand) + `ProductChangedEvent` → Storefront. Silme yok (016 sürer).
- **ModerationAgent (ayrı `reviews-moderation-agent` worker'ı):** Singleton ChatClientAgent (Temp=0,
  structured JSON, MCP'siz), retry→error queue. Moderasyon 046'da BC'den broker'lı worker'a taşındı
  (Reviews'te agent-framework yok; iletişim `ReviewModerationRequested`/`ReviewModerated` event'leriyle).

## Projeye özel yetki + tuzaklar

- **RBAC (scope; İLKE V):** `AddAuthenticationAndAuthorizationExtension(config, ...scopes)`; `KnownScopes`
  kapalı registry, rol→scope map DB'de, admin `/Admin/*`'ten yönetir. Register → `customer` rolü.
  `[RequiredScope]` Wolverine mesaj handler'larına da uygulanır. Identity.Server **HTTPS zorunlu**.
- **TUZAK (`ScopeClaimArrayHandler`):** `context.TokenType` URN'dir (`TokenTypeIdentifiers.AccessToken`),
  hint DEĞİL — hint'le kıyaslarsan handler no-op → 403 → WebApp sepet redirect döngüsü.

## Yapma listesi

- **Çok-tedarikçi feed zincirini geri getirme:** Procurement + Supplier (+ eski Supplier.Gateway /
  IngestionAgent) 050'de SÖKÜLDÜ — model first-party, mallar mağazanın. Ürün girişi = ürün-CRUD.
- **`IConfiguration`'dan doğrudan okuma** (Options pattern istisnaları hariç).
- **MCP'yi agent-dışı koddan** imperatif çağırma.
- **Yeni saga için ayrı orchestration servisi** açma (god-service) — saga sürecin sahibi BC'de host edilir.
- **Çözüme (`.slnx`) dahil olmayan klasörlere dokunma** (staging/deneme kodu) — kapsam dışı.
