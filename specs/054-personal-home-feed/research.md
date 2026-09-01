# Research: Kişisel Ana Sayfa (054)

Tüm kararlar kod tabanı incelemesine dayanır (dosya:satır referanslı). NEEDS CLARIFICATION kalmadı.

## R1 — Profil depolama biçimi: kullanıcı-başına set değil, (user, product) satırı

- **Decision**: `UserPurchase` dokümanı, satır başına bir satın alma; kompozit anahtar
  `"{userId:N}:{productId:N}"`. Alanlar: `Id`, `UserId`, `ProductId`.
- **Rationale**: Reviews aynı problemi aynı event'ten çözdü: `PurchasedProduct`
  (`src/services/reviews/Reviews.Api/Domains/Reviews/PurchasedProduct.cs`) — PK upsert =
  doğal idempotency (FR-002), tekrar teslimde aynı satır ezilir. Set-doküman yaklaşımı
  read-modify-write + concurrency derdi getirir; kuyruk Sequential olsa da PK-upsert daha basit.
  Desenin BC'ler arası tekrarı "bilinçli tekrar" kuralıyla sanksiyonlu (conventions.md).
- **Alternatives considered**: (a) Kullanıcı başına tek doküman içinde `HashSet<Guid>` —
  idempotency elle, büyüme sınırsız, reddedildi. (b) Marten event sourcing — Storefront
  projeksiyon BC'si, event store töreni gereksiz.

## R2 — Sinyal çıkarımı: event alanlarından değil, kendi StorefrontView satırlarından

- **Decision**: `OrderCompleted.Items[]`'dan yalnız `ProductId` alınır. Kategori/yazar çıkarımı
  feed sorgusu anında, satın alınan id'lerin kendi `StorefrontView` satırlarından yapılır
  (`CategoryId`, `Authors[].Id`, `FamilyCode`).
- **Rationale**: Event item'ındaki `Category?`/`Brand?` string alanları 052 öncesi mirastır
  (Brand artık modelde yok; memory: purchase Brand null enrichment gap). StorefrontView zaten
  BC'nin kendi güncel gerçeğidir; id-bazlı eşleşme string eşleşmesinden sağlam. Kontrat değişmez,
  eski tüketici kırılmaz.
- **Alternatives considered**: Event'e `CategoryId`/`AuthorIds` alanı eklemek (additive) —
  yayıncı+kontrat dokunuşu gereksiz; profil satın alma anındaki değil güncel metadata'yla
  eşleşmeli (kitap kategorisi düzelirse feed düzelir).

## R3 — Event tüketimi: mevcut kuyruğa binding + mevcut handler sınıfına overload

- **Decision**: `order.completed` exchange'i mevcut `storefront.events` kuyruğuna bind edilir
  (Program.cs, tüketici binding kurar — soğuk açılış dersi). `Handle(OrderCompleted)` overload'ı
  mevcut `StorefrontEventHandlers` sınıfına eklenir; sınıf zaten
  `opts.Discovery.IncludeType(...)` ile kayıtlı (Program.cs:54) — Wolverine keşif tuzağı yok.
- **Rationale**: Storefront'un yerleşik deseni: 3 exchange → tek Sequential kuyruk
  (Program.cs:22–55). Tek-yazar + Sequential = `UserPurchase` upsert'inde yarış yok. Yeni kuyruk
  açmak deseni bozar, sıralama garantisini böler.
- **Alternatives considered**: Ayrı `storefront.order-completed` kuyruğu (Reviews deseni) —
  Reviews'ta tek kaynak var; Storefront çok-kaynak-tek-kuyruk kurmuş, ona uyulur.

## R4 — Sıralama tiebreak: yenilik yerine puan + ad

- **Decision**: Sıra: yazar eşleşmesi > kategori eşleşmesi; eşitlikte `RatingAverage` DESC
  (null son), sonra `Name` ASC. Spec FR-009 buna göre güncellendi.
- **Rationale**: `StorefrontView`'da zaman damgası YOK (alan listesi doğrulandı) — "yeni eklenen
  önce" uygulanamaz. Alan eklemek 3 yazıcıyı ve projeksiyonu büyütür; YAGNI. Puan mevcut, kalite
  vekili; ad deterministik son kırıcı (aile/arama sıralamasında yerleşik desen: Name ASC).
- **Alternatives considered**: `ProductId` sıralı — Guid rastgele, anlamsız; `CreatedAt` alanı
  eklemek — yazım yolu dokunuşu, ertelendi.

## R5 — Aday sorgusu ve eleme

- **Decision**: Satılabilir satır filtresi liste handler'ıyla AYNI (Name/Price dolu, `!IsDeleted`;
  `GetStorefrontProductList.cs:157–206` emsali). Eşleşme: `CategoryId ∈ kategoriler` OR
  `Authors.Any(a => authorIds.Contains(a.Id))` (jsonb sorgusu 052'de canlı doğrulandı). Eleme:
  `ProductId ∈ satınAlınanlar` DIŞARI + `FamilyCode ∈ satınAlınanAileler` DIŞARI (FR-004). Aile
  gruplama + temsilci seçimi liste handler'ındaki kuralın aynısı: stokta-olan önce, ucuz önce,
  `ProductId` ASC (bellek-içi; 045 DISTINCT ON→bellek-içi kararı emsal). 12 kart (FR-010).
- **Rationale**: Vitrin tutarlılığı — kategori sayfası ile ana sayfa aynı satılabilirlik/aile
  kurallarını uygular; kural kopyası slice içinde kalır (slice bağımsızlığı, bilinçli tekrar).
- **Alternatives considered**: Liste query'sini `IMessageBus` ile çağırıp filtre bindirmek —
  slice-arası bağımlılık + iki eşleşme türünü (yazar/kategori önceliği) ifade edemez.

## R6 — Endpoint yetkisi

- **Decision**: `GET /api/v1/storefront/products/personal-feed` → `.RequireAuthorization()` +
  scope `storefront.read`; kullanıcı `CurrentUser.Load(httpContext.User)` ile çözülür. Anonim
  401/403 alır; WebApp anonim kullanıcı için endpoint'i HİÇ çağırmaz, doğrudan boş durum çizer.
- **Rationale**: Storefront auth altyapısı hazır (`AddAuthenticationAndAuthorizationExtension`,
  Program.cs:65–67, scope `AuthorizationScopes.StorefrontRead`). Kişiye-bağlı okuma kimlik ister
  (İlke V); genel vitrin uçları `AllowAnonymous` kalır.
- **Risk/doğrulama**: `customer` rolünün rol→scope map'inde `storefront.read` OLMALI — map DB'de,
  quickstart canlı doğrulamasına kontrol adımı kondu; yoksa admin ekranından işaretlenir
  (kod değişikliği değil).
- **Alternatives considered**: Yeni `storefront.personal-feed` scope'u — KnownScopes kapalı
  registry'ye ekleme gerektirir; okuma yüzeyi için granülarite fazlası, reddedildi.

## R7 — İlke VI (Domain-TDD) kapsamı

- **Decision**: Seçim/sıralama/eleme mantığı saf statik yardımcıya çekilir
  (`GetPersonalFeed` slice'ı içinde, in-memory satırlar üzerinde): eşleşme türü belirleme,
  aile eleme, temsilci seçme, sıralama, 12'ye kesme. Bu birim TEST-FIRST yazılır
  (xUnit + Shouldly, `tests/Storefront.Api.Tests` — proje yoksa açılır; 052 sonrası Storefront
  testleri kullanıcı tarafından silinmişti, yeniden doğuyor). Marten sorgusu + endpoint + WebApp
  test-sonra/canlı doğrulama.
- **Rationale**: İlke VI "mock'suz test edilebilir domain birimi" der; feed kuralları saf
  fonksiyondur. Handler'ın Marten kısmı kapsam dışı.

## R8 — WebApp boş durum + navbar

- **Decision**: `Index.cshtml`: authenticated → personal-feed çağrısı; sonuç boşsa VEYA kullanıcı
  anonim → yalnız boş durum mesajı (REV 2026-09-01: kategori kartları da kullanıcı kararıyla
  kaldırıldı; gezinme navbar'dan). "Öne Çıkan Kitaplar" bölümü,
  "Tüm Kitaplara Göz At" bağlantısı ve `_Layout.cshtml` navbar'ındaki "Tüm Kitaplar" girişi
  silinir; `/Products` sayfası ve kategori gezinmesi (navbar "Tüm Kategoriler" dahil) aynen kalır.
- **Rationale**: FR-001/FR-006/FR-007. Refit istemcisi bearer token'ı zaten enjekte ediyor
  (`AuthenticatedHttpClientHandler`, WebApp Program.cs:92) — yeni auth kablosu yok.
- **Alternatives considered**: Anonim için de endpoint çağırıp 401 yutmak — gereksiz istek +
  log gürültüsü, reddedildi.

## R9 — FLOW.md (İlke VII)

- **Decision**: `src/services/storefront/FLOW.md`'ye yeni süreç adımı: "Order: tamamlanan
  siparişten kullanıcı-satın-alma kaydı yazılır (OrderCompleted → UserPurchase)" + kişisel feed
  okuma adımı + sınır güncellemesi. Aynı PR'da.
- **Rationale**: Yeni tüketilen olay + yeni birikim = domain süreci değişimi, dar tetik kapsamında.