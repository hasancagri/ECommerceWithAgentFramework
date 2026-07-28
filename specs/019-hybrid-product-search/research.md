# Research: 019 Hibrit Ürün Araması (2026-07-28)

## R1. Marten + pgvector entegrasyonu

- **Decision**: `Marten.PgVector` 9.5.0 + `Pgvector` 0.3.2 paketleri; Storefront `AddMarten`'a `UsePgVector()`.
- **Rationale**: Resmi companion paket (JasperFx/marten reposu), sürümü core Marten 9.5.0 ile birebir.
  `UsePgVector()` NpgsqlDataSource'a vector tipini kaydeder VE `CREATE EXTENSION IF NOT EXISTS vector`'ı
  Marten şema migration'ına ekler (`ApplyAllDatabaseChangesOnStartup` mevcut) — elle DDL gerekmez.
- **Alternatives**: Elle side-table + Weasel migration (daha çok kod); EF Core (repoda yasak duruş).

## R2. Embedding saklama biçimi

- **Decision**: `ProductEmbedding` ayrı bir **Marten dokümanı** (Identity=ProductId): `TextHash` + `Embedding: float[]`
  (JSONB içinde). Sorguda `(data->>'Embedding')::vector(1536)` cast'i ile kullanılır.
- **Rationale**: StorefrontView dokümanı şişmez (1536 float her liste sorgusunda taşınmaz); upsert `session.Store`
  ile; şemayı Marten yönetir. Marten.PgVector'ın kendi `VectorProjection`'ı da aynı yan-kayıt fikrindedir.
- **Alternatives**: Vektörü StorefrontView içine gömmek (her okumayı şişirir, API response'una sızar);
  gerçek `vector(N)` kolonlu el tablosu (custom migration yükü). HNSW index şimdilik YOK — ürün sayısı
  binler mertebesinde, sequential cast-scan yeterli; büyürse expression index sonradan eklenir.

## R3. Hibrit sorgu (filtre + benzerlik) yürütme

- **Decision**: SearchText varken ham SQL: `mt_doc_storefrontview` ⋈ `mt_doc_productembedding` (ProductId),
  WHERE satılabilirlik + filtre cast'leri, `ORDER BY (e.data->>'Embedding')::vector(1536) <=> @q::vector(1536)`.
  Sorgu vektörü **text olarak** (`[f1,f2,...]`) parametre + server-side `::vector` cast.
- **Rationale**: Marten LINQ vektör ORDER BY üretemez; paketin `VectorSearchAsync`'i WHERE filtresi almaz.
  Text-param+cast, Npgsql pg_type cache yarışına karşı Marten.PgVector'ın kendi kullandığı bağışık desendir.
  SearchText yokken Marten LINQ (mevcut sorgu deseni) kalır.
- **Alternatives**: `VectorSearchAsync` + bellekte filtre (limit'ten sonra filtre → eksik sonuç; reddedildi);
  `Vector` tipini binary parametre bağlamak (cache yarışı riski).

## R4. Aspire Postgres imajı

- **Decision**: `AddPostgres("postgres").WithImage("pgvector/pgvector", "pg17")` — `WithDataVolume`'dan ÖNCE.
- **Rationale**: Aspire 13.3.5 default'u `postgres:17.6` → mevcut volume pg17 ile aynı veri yolu; pgvector/pg17
  imajı volume-uyumludur (yalnız extension binary'leri eklenir). `pg18` tag'i KULLANILMAZ: Aspire
  `WithDataVolume` tag'i sayısal parse edemeyip 17-yolu mount eder → pg18'de sessiz veri kaybı riski.
  Çağrı sırası önemli: veri yolu, `WithDataVolume` anındaki imaj annotation'ından çözülür.
- **Alternatives**: Ayrı vektör DB (Qdrant vb.) — yeni altyapı, BC içi tek-DB duruşunu bozar; reddedildi.

## R5. Embedding üretimi (API)

- **Decision**: `Microsoft.Extensions.AI.OpenAI` (CPM'de mevcut 10.7.0) —
  `new OpenAIClient(key).GetEmbeddingClient(model).AsIEmbeddingGenerator()`; üretim `GenerateVectorAsync(text)`.
  Model: `text-embedding-3-small` (1536 boyut, doğrulandı). Config: `OpenAI:ApiKey` + `OpenAI:EmbeddingModel`
  zorunlu, açılışta fail-fast (IngestionAgent deseni). Generator DI'da Singleton.
- **Rationale**: Paket ve sürüm zaten CPM'de; ChatAgent/IngestionAgent ile aynı aile. Sürüm bump gerekmez.
- **Alternatives**: OpenAI SDK'yı doğrudan kullanmak (M.E.AI soyutlaması repo genel yönelimi); lokal model (kapsam dışı).

## R6. Benzerlik eşiği

- **Decision**: Kosinüs mesafesi `<=>` ≤ **0.7** (benzerlik ≥ ~0.3) başlangıç sabiti; canlı doğrulamada kalibre.
- **Rationale**: text-embedding-3-small skorları ada-002'den belirgin düşüktür; 0.3-0.5 benzerlik bandı yaygın
  pratik. Eşik top-K altında bir taban filtresidir, hassasiyet mekanizması değil (arXiv 2408.04887).
- **Alternatives**: Eşiksiz top-K (alakasız sonuç sızar, US2-S2 ihlali); sabit yüksek eşik (boş sonuç riski).

## R7. ChatAgent / gateway entegrasyonu

- **Decision**: Gateway'de `storefront-mcp-route` ZATEN VAR (`/mcp/storefront` → storefront.cluster) — gateway
  değişikliği yok. ChatAgent: `McpServers.Storefront` sabiti + storefront URL + iki agent allowlist'i;
  public agent'tan Catalog `search_products` çıkarılır (FR-018).
- **Rationale**: Keşifle doğrulandı (appsettings.Development.json:153). Storefront `MapMcp("/mcp")` da mevcut;
  yalnız tool sınıfı eksik (`WithToolsFromAssembly` keşfeder).

## R8. Doğrulama yüzeyi

- **Decision**: Aynı query anonim REST endpoint olarak da map'lenir: `GET api/v1/storefront/products/search`.
- **Rationale**: Canlı doğrulama (quickstart) sohbet LLM'inin değişkenliğinden bağımsız olur; mevcut
  storefront read endpoint'leri de anonimdir, desenle tutarlı.