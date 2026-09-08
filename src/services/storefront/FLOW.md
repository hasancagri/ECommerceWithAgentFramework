# Storefront — Domain Süreci

**BC ne yapar:** Catalog+Stock+Reviews+Order'dan akan **şişman event'leri** ürün-anahtarlı tek satırda
(composite read-model) toplar; listeyi, facet'i, varyant ailesini, sipariş-temelli kişisel feed'i ve
asistana açık **tek serbest-sorgu kapısını** vitrine sunar.

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
10. **Varyant ailesi sunulur.** Aile eksenleri ve üyeler.               `(GetProductFamily)`
11. **Asistan sorusu TEK sorgu kapısından yanıtlanır.** Asistanın        `(AgentSqlGuard`
    kurduğu salt-okur sorgu önce bekçiden geçer (yazma/yüzey-dışı        ` → QueryStorefrontForAgent`
    istek ÇALIŞMADAN reddedilir), anlamsal metin sistemce temsile        ` → AgentQueryLog)`
    çevrilir, sorgu yalnız satılabilir yüzeyde koşar ve ret dahil
    her çağrı iz bırakır. Temalı arama + benzerlik de bu kapıdandır;
    eşik altı sonuç = "bulunamadı". Keşif envanteri Catalog'dadır.
12. **Satılabilir yüzey tek ilişki olarak kurulur.** Açılışta           `(StorefrontSellableSchema`
    satılabilirlik filtresi gömülü görünüm + tek-yetkili kısıtlı         ` → AgentQuerySurfaceBootstrap)`
    rol tazelenir; yayından kalkan ürün yüzeyde HİÇ var olmaz.
13. **Tamamlanan sipariş satın-alma kaydına döner.** Kalem başına        `(OrderCompleted`
   kullanıcı+ürün satırı; tekrar teslim/alım aynı satır (idempotent).    ` → UserPurchase)`
14. **Kişisel feed sunulur.** Satın alınan kitapların kategori+yazar    `(GetPersonalFeed`
    sinyalinden, alınmamış (aile dahil) kitaplar; yazar > kategori.      ` → RankFeed)`

## Domain kuralları (süreci yöneten değişmezler)

- **Rich aggregate DEĞİL.** `StorefrontView` invariant taşımaz; Catalog+Stock+Reviews'ün ProductId-anahtarlı tek composite satırı.
- **Kısmi satır geçerli.** Her kaynak yalnız kendi alanını yazar; `Price`/`Name` null = "Catalog raporlamadı" (dolu-satır filtresi eler).
- **Push-only, geri-çekiş YOK.** Yalnız şişman event tüketir; hiçbir kaynağa dış çağrı yapmaz (fat-event dersi).
- **Tek yazıcı + Sequential.** Dört exchange tek kuyruğa; eşzamanlı yazım = optimistic concurrency → Wolverine retry.
- **Anlamsal temsil yaşam-döngüsü taşımaz.** Ayrı yol-arkadaşı satırda yaşar; görünürlük HER ZAMAN satılabilirlik filtresinden gelir (yayından kalkan ürün temsili dursa da görünmez).
- **Alakasızlık eşiği dürüstlük kuralıdır.** Eşik altı benzerlik "bulunamadı"dır; en-yakın-ama-alakasız sonuç asla "benzer" diye sunulmaz.
- **Serbest sorgu yalnız satılabilir yüzeyi görür ve iz bırakır.** Kapı salt-okurdur; satın-alma kayıtları ve yayından kalkan ürün yüzeyin yapısal DIŞIDIR; ret dahil her sorgu kayda geçer (`AgentQueryLog`).
- **Kişisel feed kullanıcıya bağlı tek okuma yüzeyidir.** Kimlik token'dan; sinyalsiz kullanıcı boş liste alır (fallback vitrin YOK); satın alınan ürün ve varyant ailesi asla önerilmez.

## Sınır (bu BC'nin dokunmadığı)

Ürün yazımı/CRUD, fiyatlandırma, sepet, sipariş yok. `IsAvailableForSale` ayrı süreç sahipli (ingestion asla yazmaz).
