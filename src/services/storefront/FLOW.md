# Storefront — Domain Süreci

**BC ne yapar:** Catalog+Stock+Reviews'ten akan **şişman event'leri** ürün-anahtarlı tek satırda
(composite read-model) toplar; listeyi, facet'i, varyant ailesini ve hibrit aramayı vitrine sunar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Üç kaynak event'i TEK sıralı kuyruğa akar.** Catalog, Stock,       `(storefront.events`
   Reviews aynı kuyruğa bağlanır → satır yarışı yok.                     ` → Sequential)`
2. **Catalog içeriği satıra yazılır.** Ad/fiyat/marka/kategori +        `(ProductChangedEvent`
   kanonik spec'ler + varyant aile kodu, kaynak tek alan grubu.          ` → ApplyCatalog)`
3. **Stok adedi satıra yazılır.** Yalnız `StockQuantity`; arama         `(StockChangedEvent`
   metnine girmez, embedding'e BİLEREK dokunulmaz.                       ` → ApplyStock)`
4. **Puan özeti satıra yazılır.** Mutlak değer; Count=0 rozeti          `(ReviewSummaryChanged`
   temizler. Satır yoksa da kısmi satır yaratılır.                       ` → ApplyReviewSummary)`
5. **Satır her kaynak için upsert'lenir.** Herhangi bir kaynak          `(StorefrontView.Create)`
   satırı doğurabilir; her kaynak YALNIZ kendi alanını yazar.
6. **Arama metni değiştiyse anlamsal yan-kayıt tazelenir.** Ad+açıklama `(RefreshEmbeddingAsync`
   +marka+kategori hash'i; aynıysa üretim yok, hata yutulur.            ` → ProductEmbedding)`
7. **Ana sayfa/liste TEK okumayla dolar.** Dolu-satır filtresi +        `(GetStorefrontProductList)`
   spec kesişimi; aile başına tek temsilci + kart-bazlı sayfalama.
8. **Facet seçenekleri satılabilir satırlardan türetilir** (cache'li).  `(GetStorefrontFilterOptions)`
9. **Varyant ailesi + hibrit arama sunulur.** Aile eksenleri;           `(GetProductFamily,`
   filtre-yalnız veya pgvector kosinüs join yolu.                        ` SearchStorefrontProductsForAgent)`

## Domain kuralları (süreci yöneten değişmezler)

- **Rich aggregate DEĞİL.** `StorefrontView` invariant taşımaz; Catalog+Stock+Reviews'ün ProductId-anahtarlı tek composite satırı.
- **Kısmi satır geçerli.** Her kaynak yalnız kendi alanını yazar; `Price`/`Name` null = "Catalog raporlamadı" (dolu-satır filtresi eler).
- **Push-only, geri-çekiş YOK.** Yalnız şişman event tüketir; hiçbir kaynağa dış çağrı yapmaz (fat-event dersi).
- **Tek yazıcı + Sequential.** Üç exchange tek kuyruğa; eşzamanlı yazım = optimistic concurrency → Wolverine retry.
- **Stok arama metnine girmez.** `StockChangedEvent` embedding'i tetiklemez; yalnız içerik hash'i değişince üretilir (FR-013).

## Sınır (bu BC'nin dokunmadığı)

Ürün yazımı/CRUD, fiyatlandırma, sepet, sipariş yok. `IsAvailableForSale` ayrı süreç sahipli (ingestion asla yazmaz).
