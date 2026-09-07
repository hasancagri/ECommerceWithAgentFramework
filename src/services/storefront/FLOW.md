# Storefront — Domain Süreci

**BC ne yapar:** Catalog+Stock+Reviews+Order'dan akan **şişman event'leri** ürün-anahtarlı tek satırda
(composite read-model) toplar; listeyi, facet'i, varyant ailesini, filtre aramasını ve sipariş-temelli
kişisel feed'i vitrine sunar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Dört kaynak event'i TEK sıralı kuyruğa akar.** Catalog, Stock,     `(storefront.events`
   Reviews, Order aynı kuyruğa bağlanır → satır yarışı yok.              ` → Sequential)`
2. **Catalog içeriği satıra yazılır.** Ad/fiyat/yazarlar/yayınevi/      `(ProductChangedEvent`
   kategori + kanonik spec'ler + varyant aile kodu, tek alan grubu.      ` → ApplyCatalog)`
3. **Açıklama değişince anlamsal temsil tazelenir.** Karar saf:         `(DecideEmbedding`
   boş açıklama temsili siler; değişen/eksik temsil yeniden üretilir     ` → ProductDescriptionEmbedding)`
   (aynı transaction, ayrı yol-arkadaşı satır; view şişmez).
4. **Geçmiş katalog açılışta taranır.** Açıklaması dolu ama temsilsiz   `(EmbeddingBackfillService)`
   satırlar batch'lerle doldurulur; iş yoksa no-op (idempotent).
5. **Stok adedi satıra yazılır.** Yalnız `StockQuantity`; diğer         `(StockChangedEvent`
   kaynakların alanlarına dokunmaz.                                      ` → ApplyStock)`
6. **Puan özeti satıra yazılır.** Mutlak değer; Count=0 rozeti          `(ReviewSummaryChanged`
   temizler. Satır yoksa da kısmi satır yaratılır.                       ` → ApplyReviewSummary)`
7. **Satır her kaynak için upsert'lenir.** Herhangi bir kaynak          `(StorefrontView.Create)`
   satırı doğurabilir; her kaynak YALNIZ kendi alanını yazar.
8. **Ana sayfa/liste TEK okumayla dolar.** Dolu-satır filtresi +        `(GetStorefrontProductList)`
   spec kesişimi; aile başına tek temsilci + kart-bazlı sayfalama.
9. **Facet seçenekleri satılabilir satırlardan türetilir** (cache'li).  `(GetStorefrontFilterOptions)`
10. **Varyant ailesi + filtre araması sunulur.** Aile eksenleri;         `(GetProductFamily,`
   yazar/fiyat/stok filtresi (Name ASC, deterministik).                 ` SearchStorefrontProductsForAgent)`
11. **Anlamsal arama + benzerlik sunulur.** Temalı sorgu yapısal       `(SearchStorefrontProductsForAgent,`
    filtre SONRASI kNN sıralar; "buna benzer" ürünün kendi              ` FindSimilarBooksForAgent)`
    temsiliyle koşar; eşik altı sonuç = "bulunamadı". Keşif
    envanteri (kategori/yazar/yayınevi listeleri) Catalog'dadır.
12. **Tamamlanan sipariş satın-alma kaydına döner.** Kalem başına        `(OrderCompleted`
   kullanıcı+ürün satırı; tekrar teslim/alım aynı satır (idempotent).    ` → UserPurchase)`
13. **Kişisel feed sunulur.** Satın alınan kitapların kategori+yazar    `(GetPersonalFeed`
    sinyalinden, alınmamış (aile dahil) kitaplar; yazar > kategori.      ` → RankFeed)`

## Domain kuralları (süreci yöneten değişmezler)

- **Rich aggregate DEĞİL.** `StorefrontView` invariant taşımaz; Catalog+Stock+Reviews'ün ProductId-anahtarlı tek composite satırı.
- **Kısmi satır geçerli.** Her kaynak yalnız kendi alanını yazar; `Price`/`Name` null = "Catalog raporlamadı" (dolu-satır filtresi eler).
- **Push-only, geri-çekiş YOK.** Yalnız şişman event tüketir; hiçbir kaynağa dış çağrı yapmaz (fat-event dersi).
- **Tek yazıcı + Sequential.** Dört exchange tek kuyruğa; eşzamanlı yazım = optimistic concurrency → Wolverine retry.
- **Anlamsal temsil yaşam-döngüsü taşımaz.** Ayrı yol-arkadaşı satırda yaşar; görünürlük HER ZAMAN satılabilirlik filtresinden gelir (yayından kalkan ürün temsili dursa da görünmez).
- **Alakasızlık eşiği dürüstlük kuralıdır.** Eşik altı benzerlik "bulunamadı"dır; en-yakın-ama-alakasız sonuç asla "benzer" diye sunulmaz.
- **Kişisel feed kullanıcıya bağlı tek okuma yüzeyidir.** Kimlik token'dan; sinyalsiz kullanıcı boş liste alır (fallback vitrin YOK); satın alınan ürün ve varyant ailesi asla önerilmez.

## Sınır (bu BC'nin dokunmadığı)

Ürün yazımı/CRUD, fiyatlandırma, sepet, sipariş yok. `IsAvailableForSale` ayrı süreç sahipli (ingestion asla yazmaz).
