# Research: Storefront Semantic Search

**Feature**: 067 | **Date**: 2026-09-07

Kaynaklar: brainstorm kararları (memory `text-first-discovery-design-decisions`, 2026-09-04/07),
Marten.PgVector 9.5.0 paket incelemesi (XML doc + assembly metadata reflection — lokal NuGet cache),
mevcut kod keşfi (Storefront.Api, ChatAgent). Spec'teki tüm belirsizlikler burada karara bağlandı.

## R1 — Vektör saklama: AYRI yol-arkadaşı doküman (REVİZE — implement bulgusu, kullanıcı onayı 2026-09-07)

- **Decision**: `ProductDescriptionEmbedding` (PK=ProductId, `float[] Vector`) — StorefrontView'un
  yanında AYRI Marten dokümanı, aynı BC/DB; handler aynı transaction'da yazar. `opts.UsePgVector()`
  ile `vector` extension şemaya eklenir.
- **Rationale (revizyon)**: İlk karar (#8, tek alan) implement'te devrildi: 1536 float JSONB'de
  ~17KB metin eder; view içinde olsaydı TÜM tam-satır okuma yolları (liste/facet/arama, hepsi
  satırları belleğe çeker) her çağrıda ~340MB taşırdı. #8'in gerekçesi (IsDeleted senkronu) iki-adım
  sorguda geçersiz: görünürlük HER ZAMAN StorefrontView satılabilirlik filtresinden gelir; embedding
  dokümanı yaşam-döngüsü durumu TAŞIMAZ. Optimistic concurrency bilinçli yok (aynı metnin temsili —
  son yazan kazanır).
- **Alternatives**: Tek alan + tüm okuma yollarını projeksiyona çevirme (~5-6 slice refactor'ü, red);
  Marten.PgVector `VectorProjection` (event-store projection'ı ister, Storefront event-sourced
  değil, red).

## R2 — Sorgu yolu: iki adım (LINQ yapısal ön-filtre → ham SQL kNN + eşik)

- **Decision**: (1) Yapısal ön-filtre: satılabilirlik (`!IsDeleted && Name!=null && Price!=null`) +
  yapısal filtreler → aday `ProductId` kümesi. (2) Parametreli ham SQL, `ProductDescriptionEmbedding`
  tablosunda: `where id = ANY(?) and (data->>'Vector')::vector <=> ? < ? order by <aynı ifade>
  limit ?` — temsili olmayan aday kendiliğinden elenir. `find_similar_books` aynı deseni
  kendisi-hariç aday kümesiyle kullanır; dönen N id için view gövdeleri `LoadManyAsync` ile yüklenir.
- **Rationale**: Paket API'si doğrulandı — `VectorSearchAsync<T>(session, expr, vector, limit,
  distanceFn)` yalnız `IReadOnlyList<T>` döner: predicate parametresi YOK, mesafe değeri YOK. Hibrit
  ön-filtre (kilitli karar #7) ve eşik (FR-005) bu API ile kurulamaz. LINQ ön-filtre mevcut agent
  slice'ının kanıtlı semantiğini (case-insensitive yazar eşleşmesi, jsonb `Authors` Any) yeniden
  SQL'de yazmadan aynen kullanır; SQL parçası sabit ve küçük kalır.
- **Alternatives**: `VectorSearchAsync` + bellekte post-filter (yapısal-önce kararına aykırı, seçici
  filtrede N dolmaz, red); tek dinamik SQL (yazar case-insensitive jsonb eşleşmesini SQL'de
  yeniden kurmak kırılgan, red); tüm embedding'leri belleğe çekip local kNN (120MB+, red).
- **Not**: id-array parametresi filtre yokken ~20k Guid olabilir — Postgres `ANY(array)` için kabul
  edilebilir; tipik semantik sorgular yapısal daraltma taşır.

## R3 — Embedding üretimi: OpenAI `text-embedding-3-small`, `IEmbeddingGenerator`

- **Decision**: `Microsoft.Extensions.AI.OpenAI` (10.7.0, props'ta pinli) →
  `new OpenAIClient(ApiKey).GetEmbeddingClient(Model).AsIEmbeddingGenerator()` singleton. Model
  `text-embedding-3-small`, 1536 boyut. Options: `OpenAiOption { ApiKey [Required], EmbeddingModel }`,
  `BindConfiguration("OpenAI") + ValidateOnStart` (ChatAgent emsali, fail-fast).
- **Rationale**: Proje zaten OpenAI'ye bağlı; embedding "agent" davranışı değil (reasoning yok, düz
  deterministik API) → ayrı worker DEĞİL, Storefront'un kendisi üretir (kilitli karar, AÇIK-2026-09-07
  çözümü). Maliyet: tüm katalog ~0.10-0.15$, önemsiz.
- **Alternatives**: Ayrı embedding worker'ı (notification-agent paterni — event üretimi/worker töreni
  gereksiz, red); Marten.PgVector `IEmbeddingProvider` arayüzü (yalnız VectorProjection ile anlamlı, red).
- **Etki**: Storefront OpenAI fail-fast listesine girer → `dotnet user-secrets set OpenAI:ApiKey`
  Storefront.Api için de gerekir; CLAUDE.md güncellenir.

## R4 — Üretim zamanlaması: ProductChangedEvent handler'ında senkron, yalnız değişimde

- **Decision**: Handler `ApplyCatalog` öncesi eski `Description`'ı okur; **yeniden-embedding kararı**
  saf yardımcıyla verilir (test-first): yeni açıklama boş → embedding null; açıklama değişti VEYA
  (açıklama dolu VE embedding null) → üret; aksi halde dokunma. `IsDeleted` embedding'i ETKİLEMEZ —
  görünürlük sorgu tarafındaki satılabilirlik filtresinde (FR-007 böyle sağlanır; yayına dönüşte
  yeniden üretim gerekmez).
- **Rationale**: Kilitli karar (senkron, Storefront içinde). Değişim kontrolü gereksiz API çağrısını
  keser (fiyat/stok güncellemeleri embedding tetiklemez). Hata → exception → Wolverine retry/error
  queue; upsert idempotent, event yeniden işlenir. API kesintisinin structural sync'i de geciktirmesi
  bilinçli kabul (basitlik > kısmi-yazma karmaşası).
- **Alternatives**: Hash-bazlı cache (YAGNI, kilitli karar); embedding'i ayrı local-queue komutuna
  atmak (kısmi-tutarlılık + sıra karmaşası, red).

## R5 — Benzerlik eşiği: ZORUNLU, konfigüre edilebilir kosinüs mesafesi

- **Decision**: `SemanticSearchOption.MaxCosineDistance` (default **0.68** — canlı kalibrasyon 2026-09-07; 0.55 TR↔EN cross-lingual mesafeyi eliyordu) — SQL WHERE'de uygulanır;
  eşiği geçen sonuç yoksa tool `Found=false` + boş liste döner, LLM'e "bulunamadı" söyletilir
  (prompt'ta). Değer quickstart'ta gerçek açıklama verisiyle kalibre edilir (tek satır config).
- **Rationale**: kNN eşiksiz hep Top-N döner → SC-005 ("alakasız sonucu benzer gibi sunma") ihlal
  olurdu. Brainstorm'da AÇIK kalan tek soru buydu; karar: eşik VAR, değeri ayarlanabilir.
  `text-embedding-3-small` için 0.4-0.6 bandı yaygın pratik başlangıçtır.
- **Alternatives**: Eşiksiz Top-N (FR-005'e aykırı, red); alaka kararını LLM'e bırakmak
  (halüsinasyon/grounding riski, red).

## R6 — Backfill: startup BackgroundService (idempotent tarama)

- **Decision**: `EmbeddingBackfillService : BackgroundService` — açılışta `Description` dolu +
  temsil dokümanı OLMAYAN satırları sayfalayıp `IEmbeddingGenerator.GenerateAsync(batch)` ile
  (batch ~500 metin) doldurur; kalan 0 olana dek döner, sonra biter. Her açılışta çalışır, iş yoksa
  no-op. Hata host'u durdurmaz (StopHost tuzağı — logla/çık, sonraki açılış toparlar).
- **Rationale**: FR-008/SC-004 — 20k kayıt ≈ 40 batch çağrısı → dakikalar. Docker reset + reseed
  sonrası kendiliğinden iyileşir (elle tetik yok). Yeni endpoint/scope töreni doğurmaz (İLKE V'e
  dokunmaz). OpenAI embedding API'si istek başına çoklu input destekler.
- **Alternatives**: Admin REST endpoint (yeni scope/kontrat + elle tetik yükü, red); tek seferlik
  script (reset sonrası tekrarlanamaz, red); Wolverine scheduled job (sürekli zamanlama gereksiz, red).

## R7 — Index: HNSW denenir, 20k ölçeğinde exact scan kabul

- **Decision**: `PgVectorOptions.VectorOn<StorefrontView>(x => x.DescriptionEmbedding, 1536,
  DistanceFunction.Cosine, ...)` kaydı implement'te denenir (paket vector kolon + ops-class
  taşıyor); üretilen şema doğrulanır. Kurulamazsa exact scan ile kalınır — 20k satırda kabul.
- **Rationale**: pgvector index'siz sequential scan yapar; 20k × 1536 boyutta bu ms mertebesidir,
  MVP için yeterli. HNSW "bedavaya geliyorsa" alınır, kendisi için mühendislik yapılmaz.
- **Alternatives**: Elle DDL migration (Marten `ApplyAllDatabaseChangesOnStartup` düzenini kırar,
  ancak VectorOn çalışmazsa `Storage.ExtendedSchemaObjects` yedek yol olarak not edilir).

## R8 — Keşif listeleri: 3 izole agent slice — REVİZE (2026-09-07, kullanıcı kararı): CATALOG'da

> İlk karar Storefront'tu (satılabilirlik gerçeği + liste↔arama tutarlılığı). Kullanıcı SRP/BC
> argümanıyla Catalog'a taşıttı: Author/Publisher/Category otoritesi orası, kategori AĞACI yalnız
> orada (ParentCategoryId), gelecek yazar özellikleri de oraya gidecek. FR-006 `Published` ürün
> filtresiyle korunur (saniyelik event-lag penceresi bilinçli kabul). Aşağıdaki orijinal karar tarihçe:

- **Decision**: `list_categories` / `list_authors` / `list_publishers` — her biri kendi
  `Features/Agents/*ForAgent.cs` slice'ı (agent-slice izolasyon konvansiyonu; facet query'si
  IMessageBus ile YENİDEN KULLANILMAZ). Kaynak küme: dolu-satır + `!IsDeleted`. Dönen: Id, Name,
  ProductCount. `list_authors` binlerce yazar için `search` (default "") + `maxResults` (default 50)
  + `TotalCount` taşır; kategori/yayınevi tam liste. Sorgular `[Cached("filters", 60)]` alabilir
  (kendi verisi, herkese-aynı, bayat-toleranslı — mevcut facet cache etiketiyle aynı invalidasyon).
- **Rationale**: Bilinen keşif kırığını (memory `chat-agent-discovery-gaps` #1) kapatır; Storefront
  Catalog'un aksine yalnız satılabilir olanı gösterir (FR-006, SC-003).
- **Alternatives**: Catalog'un REST kategori listesine MCP sarmalayıcı (yayından-kaldırılmışları da
  listeler, FR-006 ihlali, red).

## R9 — Ham SQL'de JSON casing

- **Decision**: Storefront Marten'i `UseNewtonsoftForSerialization` default casing ile kurulu →
  üye adları JSONB'de olduğu gibi (PascalCase): `data->'DescriptionEmbedding'`. Implement'te ilk
  sorguda psql ile doğrulanır.
- **Rationale**: SQL parçası tek yerde; yanlış casing sessiz boş sonuç üretir — bilinçli doğrulama adımı.

## MCP tool parametre kuralı (tuzak)

Tüm yeni/genişleyen tool parametrelerine **default değer ZORUNLU** (nullable yetmez) — LLM parametre
atlarsa `ArgumentException` (memory `mcp-tool-optional-param-default`). Kontratlar buna göre yazıldı.

## Veri ön-koşulu (spec Assumption'ı ile uyumlu)

Açıklama metni bugün %0 (books.json'da yok; OpenLibrary fetch ayrı işte koşuyor). Yapısal parçalar
(listeler, dışlama, kategori/yayınevi filtresi) veriden bağımsız hemen doğrulanır; semantik senaryolar
description'lı reseed sonrası doğrulanır. Bu feature'ın tamamlanma kriteri description doldurma DEĞİL.