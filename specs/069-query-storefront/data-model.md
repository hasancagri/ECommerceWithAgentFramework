# Data Model: Query Storefront

**Feature**: 069 | Kararlar: [research.md](research.md) R1/R6/R9

## 1. `storefront_sellable` VIEW (yeni — izinli vitrin yüzeyi, FR-005)

Şema: `storefrontmanagement`. Kaynak: `mt_doc_storefrontview` (satılabilirlik filtresi gömülü:
`NOT IsDeleted AND Name IS NOT NULL AND Price IS NOT NULL`) LEFT JOIN
`mt_doc_productdescriptionembedding`. DDL, `StorefrontSellableSchema` tek-kaynağından üretilir (R8).

| Kolon | PG tipi | Kaynak (jsonb) | Not |
|---|---|---|---|
| `product_id` | `uuid` | `id` | Sepet/benzerlik/kendisi-hariç için şart |
| `name` | `text` | `data->>'Name'` | |
| `description` | `text` | `data->>'Description'` | |
| `authors` | `text[]` | `data->'Authors'` ad projeksiyonu | Yalnız adlar; `unnest`/`ILIKE ANY` kalıbı (R9) |
| `publisher` | `text` | `data->>'Publisher'` | Id'siz (R9) |
| `category` | `text` | `data->>'Category'` | Id'siz; Storefront'ta ağaç yok (düz ad) |
| `price` | `numeric` | `data->>'Price'` | |
| `stock` | `integer` | `data->>'StockQuantity'` | NULL = stok bilgisi akmadı |
| `rating_average` | `numeric` | `data->>'RatingAverage'` | NULL = hiç puan yok |
| `rating_count` | `integer` | `data->>'RatingCount'` | |
| `specs` | `jsonb` | `data->'Specs'` | `[{Attribute,Option}]` (SpecPair gerçek şekli); varyant/özellik soruları |
| `family_code` | `text` | `data->>'FamilyCode'` | Varyant gruplama |
| `image_url` | `text` | `data->>'ImageUrl'` | |
| `added_at` | `timestamptz` | `mt_last_modified` | YAKLAŞIK ekleniş (spec edge-case kabulü) |
| `embedding` | `vector` | sidecar `data->>'Vector'` cast | NULL olabilir; yanıttan ayıklanır (R5) |

**Yapısal güvence**: kısıtlı rolün TEK yetkisi bu view'ın SELECT'i → `UserPurchase`, `AgentQueryLog`,
ham dokümanlar ve `IsDeleted` satırları yüzeyin dışında (SC-003/SC-004 yapısal).

## 2. `AgentQueryLog` (yeni Marten dokümanı — sorgu izi, FR-006)

Aggregate DEĞİL (davranışsız iz kaydı). Sahip bağlantıyla yazılır; ret dahil her çağrıda bir satır.

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | |
| `Sql` | `string` | Ham metin, `{{EMBED}}` İKAMESİZ (asistanın yazdığı hal) |
| `Verdict` | enum `Executed / Rejected / Failed` | Rejected = bekçi/rol reddi; Failed = DB hatası/timeout |
| `RejectCode` | `string?` | `StorefrontResourceConstants.AgentSql*` sabiti |
| `ErrorDetail` | `string?` | Tam Postgres/SQLSTATE metni (yalnız iz; asistana budanmış döner, R5) |
| `RowCount` | `int` | Kırpma öncesi sayılan (tavan+1 dahil) |
| `Truncated` | `bool` | Tavan kırpması oldu mu |
| `DurationMs` | `int` | |
| `CreatedAt` | `DateTimeOffset` | |

## 3. `AgentQueryOption` (yeni Options POCO — konvansiyon: BindConfiguration + ValidateOnStart)

| Alan | Default | Not |
|---|---|---|
| `MaxRows` | 50 | LIMIT sarmalama tavanı (R4) |
| `TimeoutSeconds` | 3 | `SET LOCAL statement_timeout` |
| `MaxSqlLength` | 4000 | Bekçi uzunluk tavanı |
| `RoleName` / `RolePassword` | — `[Required]` | Kısıtlı rol kimliği (R2); şifre user-secrets/env |

## 4. Silinenler (tam ikame, FR-001)

- Slice'lar: `SearchStorefrontProductsForAgent`, `FindSimilarBooksForAgent` (+ MCP sarmalayıcıları).
- `ProductEmbeddingKnnQuery` (kNN artık prompt'taki SQL kalıbı; `ToVectorLiteral` yardımcısı
  `AgentSql/`e taşınır — R3).
- `Options/SemanticSearchOption.cs` (eşik 0.68 prompt kalıbına gömülür — R7).
- ChatAgent: `ConstValues.StorefrontTools` 2 sabit → 1 (`QueryStorefront`); iki allowlist güncellenir.

## 5. Değişmeyenler (FR-009 sınırı)

`StorefrontView` + `ProductDescriptionEmbedding` dokümanları ve yazım yolları (event handler'lar,
backfill), Catalog `list_*` tool'ları, iç okuma slice'ları (liste/facet/feed), `UserPurchase`.