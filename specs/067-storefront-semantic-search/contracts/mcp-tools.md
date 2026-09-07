# MCP Tool Kontratları: Storefront Semantic Search

**Feature**: 067 | Yüzey: Storefront `/mcp` (arama+benzerlik) + **Catalog `/mcp` (keşif listeleri —
taşıma, kullanıcı kararı 2026-09-07: envanter otoritesi Catalog)**; ikisi de anonim. Tüketici:
ChatAgent (public: storefront 2 tool + catalog 3 list; assistant: +search_products/get_product) + dış agent'lar.

**TUZAK (geçerli her tool'a):** Her opsiyonel parametreye DEFAULT değer zorunlu (nullable yetmez) —
LLM parametreyi atlarsa `ArgumentException` (memory `mcp-tool-optional-param-default`).

**Ortak davranış:** Tüm tool'lar yalnız satılabilir küme üzerinde çalışır
(`!IsDeleted && Name != null && Price != null`). Boş sonuç = `Found=false` + boş liste; hata değil.

## 1. `search_storefront_products` (GENİŞLER — mevcut tool)

Slice: `Features/Agents/SearchStorefrontProducts.cs`

| Parametre | Tip | Default | Anlam |
|---|---|---|---|
| `authors` | `string[]` | `[]` | Yazar adları, aralarında OR, case-insensitive tam ad (mevcut) |
| `excludeAuthors` | `string[]` | `[]` | YENİ — bu yazarları içeren ürünleri ele (FR-003) |
| `publisher` | `string` | `""` | YENİ — yayınevi adı filtresi (case-insensitive) |
| `excludePublishers` | `string[]` | `[]` | YENİ — bu yayınevlerini ele (FR-003) |
| `category` | `string` | `""` | YENİ — kategori adı filtresi (case-insensitive) |
| `minPrice` / `maxPrice` | `decimal` | `0` | Fiyat aralığı; 0 = kısıt yok (mevcut) |
| `minStock` | `int` | `0` | ≥1 verilirse stok şartı (mevcut) |
| `semanticQuery` | `string` | `""` | YENİ — bulanık/temalı ifade. Dolu ise: yapısal filtre SONRASI kalan kümede kosinüs kNN sıralar + eşik uygular (FR-002). Boş ise mevcut davranış (Name ASC). |
| `maxResults` | `int` | `8` | 1–20 (mevcut) |

- LLM sözleşmesi (prompt'ta): kullanıcı cümlesinin yapısal kısmı yapısal parametrelere, anlamca
  kısmı `semanticQuery`'ye ayrıştırılır; Storefront ham cümle GÖRMEZ.
- `semanticQuery` dolu + eşik altında sonuç yok → `Found=false` (LLM "bulunamadı" der, SC-005).
- Response: `Found`, `Items[]` (ProductId, Name, Authors, Publisher, Category, Price,
  StockQuantity, ImageUrl). `DetailUrl` KALDIRILDI (066 sonrası ölü route — kullanıcı isteği);
  kapak görseli `ImageUrl` ile döner, LLM markdown görsel olarak gösterebilir.

## 2. `find_similar_books` (YENİ)

Slice: `Features/Agents/FindSimilarBooks.cs`

| Parametre | Tip | Default | Anlam |
|---|---|---|---|
| `productId` | `Guid` | — (zorunlu) | Referans ürün |
| `maxResults` | `int` | `8` | 1–20 |

- Sorgu vektörü = ürünün DB'deki KENDİ embedding'i (yeni OpenAI çağrısı YOK).
- Aday küme: satılabilir + embedding dolu + `id != productId` (FR-004); eşik aynı
  `MaxCosineDistance` (SC-005).
- Referans ürün yok / embedding'i null → `Found=false` + açıklayıcı mesaj kodu (Result pattern,
  exception yok — SC-002 "hata vermez").
- Response: `Found` + `Items[]` — kontrat #1 ile AYNI şekil (ProductId, Name, Authors, Publisher,
  Category, Price, StockQuantity, ImageUrl).

## 3. `list_categories` (YENİ)

Slice: `catalog/.../Categories/Features/Agents/ListCategoriesForAgent.cs` (TAŞINDI)

Parametre yok. Response: `Items[]` (CategoryId, Name, **ParentCategory** (ağaç — yalnız Catalog bilir),
ProductCount) — yalnız YAYINDA (Published) en az bir üründe kullanılan kategoriler (FR-006 ruhu;
fark: vitrin-satırı yerine yayın bayrağı, saniyelik event-lag penceresi kabul). `[Cached("agent-lists", 60)]`.

## 4. `list_authors` (YENİ)

Slice: `catalog/.../Authors/Features/Agents/ListAuthorsForAgent.cs` (TAŞINDI; filtre=Published ürün)

| Parametre | Tip | Default | Anlam |
|---|---|---|---|
| `search` | `string` | `""` | Ad alt-dizge filtresi (case-insensitive); boş = tümünden ilk sayfa |
| `maxResults` | `int` | `50` | 1–200 |

Response: `Items[]` (AuthorId, Name, ProductCount) + `TotalCount` (LLM "hepsi bu değil" diyebilsin).
Sıralama: ProductCount DESC (çok kitaplı yazar önce — keşif değeri).

## 5. `list_publishers` (YENİ)

Slice: `catalog/.../Publishers/Features/Agents/ListPublishersForAgent.cs` (TAŞINDI; filtre=Published ürün)

| Parametre | Tip | Default | Anlam |
|---|---|---|---|
| `search` | `string` | `""` | Ad alt-dizge filtresi |
| `maxResults` | `int` | `100` | 1–200 |

Response: `Items[]` (PublisherId, Name, ProductCount) + `TotalCount`. Sıralama ProductCount DESC.

## ChatAgent wiring (kontratın tüketici tarafı)

- `ConstValues.StorefrontTools`: 4 yeni sabit; public + assistant allowlist'lerine beşi de girer.
- Prompt güncellemeleri: (a) keşif soruları list_* tool'larına yönlenir; (b) tür/tema ifadesi kriter
  SAYILIR → `semanticQuery`'ye (kriter dilenme kuralı gevşer — bilinen bulgu #2); (c) `Found=false`
  → dürüst "bulunamadı", asla uydurma öneri (RAG/grounding duruşu).
- Cross-facet VEYA ("kategori X veya yazar Y") tek sorguda ÇÖZÜLMEZ — agent birden çok çağrı yapıp
  birleştirir (spec kapsam-dışı kararı; prompt'ta örneklenir).