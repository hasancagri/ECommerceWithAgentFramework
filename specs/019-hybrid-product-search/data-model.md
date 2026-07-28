# Data Model: 019 Hibrit Ürün Araması

## Mevcut (değişmiyor): StorefrontView

`storefrontManagement` şemasında Marten dokümanı; kimlik `ProductId`, optimistic concurrency açık.
Aramanın tek veri kaynağı. Alanlar: Name, Description, Price, BrandId/Brand, CategoryId/Category,
ImageUrl, StockQuantity, IsDeleted. Bu feature alan eklemez.

Satılabilirlik kuralı (mevcut sorgularla aynı): `Price != null && Name != null && !IsDeleted`.

## Yeni: ProductEmbedding (ürün anlamsal verisi)

Ürün başına bir kayıt; `storefrontManagement` şemasında ayrı bir **Marten dokümanı**
(Identity=ProductId). Vektör JSONB'de float dizisi olarak durur; sorguda `::vector(1536)`
cast'iyle kullanılır (research R2/R3). Aggregate DEĞİL, read-model yan dokümanıdır.

| Alan | Tip | Açıklama |
|---|---|---|
| ProductId | Guid (Identity) | StorefrontView.ProductId ile bire bir |
| TextHash | string | Arama metninin SHA-256'sı; değişim tespiti |
| Embedding | float[] (1536) | text-embedding-3-small çıktısı |
| UpdatedTime | DateTimeOffset | Son üretim zamanı |

- Arama metni: `Name + "\n" + Description + "\n" + Brand + "\n" + Category` (null'lar atlanır).
- Üretim tetikleyicisi: `ProductChangedEvent` handler'ı view'ı kaydettikten sonra hash'i karşılaştırır;
  değiştiyse embedding üretir ve upsert eder. Hash aynıysa hiçbir şey yapmaz (FR-013).
- `StockChangedEvent` embedding'e dokunmaz (FR-013/SC-004).
- Üretim hatası: loglanır, view kaydı etkilenmez (FR-014); kayıt eksik kalır → ürün anlamsal
  sıralamaya girmez; sonraki `ProductChangedEvent`'te hash farkı yeniden üretimi tetikler (FR-015).
- Satır silinmez; view `IsDeleted` olduğunda arama zaten satılabilirlik filtresiyle eler.

## Arama isteği: SearchStorefrontProductsQuery

| Alan | Tip | Kural |
|---|---|---|
| Brands | string[]? | OR birleşimi; ad eşleşmesi case-insensitive tam ad |
| MinPrice | decimal? | >= 0; MaxPrice ile birlikte verilirse MinPrice <= MaxPrice |
| MaxPrice | decimal? | >= 0 |
| MinStock | int? | "stokta en az N"; >= 1 |
| SearchText | string? | Anlamsal sorgu metni |
| MaxResults | int? | Varsayılan 8; 1..20'ye kırpılır |

Doğrulama: en az bir kriter (Brands/MinPrice/MaxPrice/MinStock/SearchText) zorunlu (FR-003);
MinPrice > MaxPrice hata Result'ı (edge case).

## Arama sonucu: SearchStorefrontProductsResponse

Liste öğesi: `ProductId, Name, Brand, Category, Price, StockQuantity, DetailUrl`.
`DetailUrl` biçimi Catalog SearchProducts ile aynı: `/Products/Detail/{ProductId}`.
Boş liste → `FeatureListResultModel.NotFound` (FR-016).

## Sorgu semantiği

- SearchText yok: Marten LINQ; filtreler AND, deterministik sıra (Name ASC) (FR-008).
- SearchText var: sorgu embed edilir; tek SQL'de StorefrontView doküman tablosu ⋈ ProductEmbedding,
  WHERE (satılabilirlik + filtreler), ORDER BY kosinüs mesafesi, benzerlik eşiği altı elenir (FR-006/007).
- Embedding'i olmayan ürün anlamsal aramada sonuçlara giremez (join iç birleşim) (US2-S3).