# Research: 041 Multi-Supplier Dropship — Procurement BC

Tüm NEEDS CLARIFICATION spec/clarify oturumunda kapandı; buradaki kararlar tasarım düzeyi seçimlerdir.

## R1 — Havuz aggregate tasarımı: PoolProduct (barkod-Id) + SupplierListing entity

- **Decision**: Tek aggregate `PoolProduct` (Marten string identity = barkod). Child entity `SupplierListing`
  (tedarikçi başına ham içerik + fiyat + stok + içerik hash + Delisted). Kanonik içerik, durum makinesi ve buy-box
  aynı aggregate'te. Spec'in `SupplierOffer`'ı listing'in fiyat/stok yüzüdür (ayrı aggregate AÇILMAZ).
- **Rationale**: Buy-box ve merge, barkodun TÜM listinglerini birlikte görmek zorunda — tutarlılık sınırı barkod.
  Tek aggregate = atomik karar, yarış yok, saf domain birim testi kolay (İlke VI).
- **Alternatives**: (a) Ayrı SupplierOffer aggregate — cross-aggregate okuma + çift yazım; red. (b) Ham satır ayrı
  landing dokümanı + kanonik ayrı — diff/merge iki yere bölünür, durum makinesi parçalanır; red.

## R2 — Tie-break kimliği: seed'li Supplier.Priority

- **Decision**: `Supplier` aggregate seed'lenir (Guid Id + benzersiz `int Priority`; A=1, B=2). Spec'teki
  "düşük SupplierId kazanır" = düşük Priority. `BuyBoxChanged.SupplierId` Guid taşır.
- **Rationale**: Guid'ler sıralanamaz/deterministik değil; Priority insan-okur ve seed'le sabit. İşleme sırasından
  bağımsızlık (SC-008) ancak statik kimlikle sağlanır.
- **Alternatives**: Üretim-sırası SupplierId — sıraya bağımlı, clarify'da reddedildi.

## R3 — Kanonik taksonomi sahipliği: iki BC'de ayrı seed, sözleşme = AD

- **Decision**: Kanonik Category>SubCategory ağacı Catalog'da seed'lenir (Category.ParentCategoryId zaten var).
  Procurement KENDİ kopyasını seed'ler (eşleme hedefi + enrich'in seçim listesi). Event kanonik AD çifti taşır
  (Category + SubCategory string); Catalog `NormalizedName` ile çözer. Çözülemeyen ad → hata + error queue
  (seed hizasızlığı bug'ıdır, veri durumu değil).
- **Rationale**: İlke I — ortak domain modeli yok; CategoryId bir BC'den diğerine sızamaz. Ad tekrarı, hata kodu
  tekrarı gibi bilinçli BC-izolasyon maliyetidir.
- **Alternatives**: Shared taksonomi sabiti — ortak model sızıntısı; red. Procurement'ın CategoryId sorması (REST) —
  yapısal bağımlılık + LLM'siz akışta gereksiz senkron kanal; red.

## R4 — Event akışı ve yarış çözümü: fat CanonicalProductUpserted (fiyat+stok dahil)

- **Decision**: Üç yeni kontrat:
  1. `CanonicalProductUpserted` — Procurement→Catalog; içerik + Sku + ölçü + o anki buy-box Price+Stock (fat).
  2. `BuyBoxChanged {Barcode, SupplierId, Price, Stock}` — Procurement→Catalog+Stock; yalnız kazanan/fiyat/stok değişince.
  3. `ProductLinked {Barcode, ProductId, InitialStock}` — Catalog→Stock; ürün oluşunca eşleme + ilk OnHand.
  Catalog: canonical → Product upsert (Gtin=barkod) + `ProductChangedEvent` (mevcut kontrat, Storefront değişmez) +
  `ProductLinked`. Stock: ProductLinked → BarcodeLink doc + OnHand; BuyBoxChanged → map'ten OnHand mutlak yaz +
  `StockChangedEvent`. Stock'a barkodu bilinmeyen BuyBoxChanged gelirse yok sayılır — ilk değer ProductLinked'te
  taşındığından kayıp olmaz (yarış edge'i kapanır).
- **Rationale**: Fat event = Storefront'un mevcut deseni (R7/006); ilk stok ProductLinked'e binince "BuyBoxChanged
  ürün yokken geldi" yarışı ortadan kalkar. Dış kontrat `ProductChangedEvent`/`StockChangedEvent` SABİT kalır —
  Storefront/WebApp bu feature'da değişmez.
- **Alternatives**: İnce canonical + fiyatı yalnız BuyBoxChanged'te taşımak — üründe fiyatsız pencere + sıra
  garantisi ihtiyacı; red. Stock'un canonical'ı da dinlemesi — barkod→ProductId çevirisini yapamaz; red.

## R5 — Enrich agent: in-process ChatClientAgent, MCP'siz, lokal durable kuyruk

- **Decision**: `EnrichmentAgent` Procurement içinde Singleton ChatClientAgent (Microsoft.Agents.AI +
  Microsoft.Extensions.AI.OpenAI; `OpenAI:ApiKey`+`Model` fail-fast, Temperature=0, structured JSON çıktı).
  Girdi: eksik alan listesi + mevcut içerik + kanonik kategori listesi. Çıktı: yalnız eksik İÇERİK alanları
  (açıklama, kategori seçimi — listeden). Barkod/ölçü/fiyat/stok İSTENMEZ ve yazılmaz (aggregate guard'ı da reddeder).
  Tetik: Wolverine lokal durable kuyruğa `EnrichPoolProduct {Barcode}`; retry 10s/30s/60s → error queue (DLQ deseni
  IngestionAgent'tan miras). Sonuç `PoolProduct.ApplyEnrichment` ile yazılır + kaynak hash saklanır (cache).
- **Rationale**: Kendi BC verisi — MCP gerekmez (MCP yalnız agent-tüketir kuralına da uygun: tüketilen MCP yok).
  Lokal kuyruk = feed işleme hızını AI gecikmesine bağlamaz; yapısal yol AI'sız kalır (FR-017).
- **Alternatives**: Senkron enrich (pull sırasında) — 300 satır × LLM gecikmesi pull'u kilitler; red.
  MCP üzerinden Catalog'a yazan agent (015 deseni) — sökülen desenin geri gelişi; red.

## R6 — Mock feed: rev başına statik JSON dataset (kullanıcı kararı 2026-08-19)

- **Decision**: Veri `Datasets/supplier-{kod}.rev{N}.json` dosyalarında yaşar (script-üretimli, commit'li,
  elle düzenlenebilir). Endpoint dosyayı istek anında okur (değişiklik restart'sız yansır — 005/R12 mirası).
  Kimlik uzayı: 8690000000001..3000; A = 1..1300 benzersiz + 2501..3000 çakışan (1800 satır), B = 1301..2500
  benzersiz + 2501..3000 çakışan (1700 satır) → çakışan=500, benzersiz toplam=3000. Çakışanlarda dağılım:
  fiyat ~%45 A / ~%45 B ucuz, ~%10 eşit; ~%10'unda en ucuz aday stok 0; ad/açıklama hafif farklı; iki tedarikçi
  FARKLI kategori adları. ~%10 satırda açıklama ve/veya kategori eksik. Ölçü ürün türüne uygun sabit bantlar.
  `rev` başına AYRI dosya: rev2 yalnız fiyat/stok sapması taşır (içerik alanları rev1 ile birebir; SC-004).
  Supplier.Api tedarikçi başına bellek-içi güncel rev tutar: `GET /v1/feeds/{supplier}` güncel rev dosyasını döner,
  `POST /v1/feeds/{supplier}/advance` rev'i artırır (dosyası olmayan rev mevcut en yükseğe düşer).
  Eski `GET /v1/feeds` + `products.json` SİLİNİR. Dataset kontratı test-first doğrulanır (FeedDatasetTests).
- **Rationale**: Statik dosya = determinizm bedava (idempotency SC-007, sıra-bağımsızlık SC-008); veri gözle
  görülür/elle düzenlenebilir (kullanıcı tercihi); rev = "feed değişti"yi tek POST'la simüle etmek.
- **Alternatives**: Kod-içi deterministik üretici — kullanıcı örnek veriyi JSON dosyası olarak istedi; red.
  Zaman-bazlı rastgelelik — determinizm ölür; red.

## R7 — Söküm kapsamı

- **Decision**: Silinen: `Supplier.Gateway` projesi (+ `supplierGatewayDb`, Hangfire'ı, FeedSnapshot),
  `IngestionAgent` projesi (MAF workflow + yazıcı agent'lar), `SupplierProductSnapshotReceived` kontratı,
  `RabbitMqConstants.SupplierProductSnapshot`, Catalog `UpsertBrand/UpsertCategory/UpsertProduct` agent slice'ları +
  MCP tool'ları, Stock `SetStock` agent slice + MCP tool, AppHost `supplier-gateway`+`ingestion-agent` kayıtları,
  `IngestionWriteException` (Common). Kalan: Catalog/Stock okuma MCP tool'ları (ChatAgent kullanır), `set_stock`
  DIŞINDAKİ Stock yüzeyi, Supplier.Api projesi (mock olarak büyür).
- **Rationale**: Clarify kararları; tek yazım yolu Procurement event'leri olunca agent-yazım yüzeyi ölü kod.
- **Alternatives**: Gateway'i ince çekirdek olarak tutmak — kullanıcı da sökümü istedi (2026-08-19); red.

## R8 — Feed pull altyapısı: Hangfire cron Procurement'ta (Gateway deseni miras)

- **Decision**: Hangfire (Postgres storage, `procurementDb`/hangfire şeması) + `Feeds:PullCron` (30dk) +
  açılışta gecikmeli ilk pull + `POST /v1/feeds/pull` manuel tetik (anonim, dev aracı; SemaphoreSlim tek-uçuş).
  Tedarikçi listesi seed'den; URL'ler Aspire service discovery (`services:supplier-api:http:0` — Options istisnası).
  Pull akışı: her tedarikçi için fetch → adapter parse → satır başına `PoolProduct` upsert (hash-diff) → değişenlere
  publish/enrich zinciri. Feed'de GÖRÜNMEYEN barkod listing'i `MarkDelisted` (full-snapshot feed varsayımı).
- **Rationale**: 007/008'de kanıtlanmış desen; tek fark hedefin event yerine kendi aggregate'i olması.
- **Alternatives**: Wolverine scheduled message — Hangfire zaten evde ve pano/tekrar-çalıştırma veriyor; gerek yok.

## R9 — İçerik birleştirme kuralı (clarify onayı)

- **Decision**: Alan bazında: Priority sırasına göre ilk DOLU değer kazanır (A=1 önce); eksik alan sonraki
  tedarikçiden dolar; hâlâ eksikse enrich. Delisted listing birleşmeye girmez. Yeniden hesap her listing
  değişiminde koşar → sonuç sıra-bağımsız (aynı listing kümesi = aynı kanonik).
- **Rationale**: first-writer-wins reddedildi (sıra-bağımlı); Priority-merge deterministik ve test edilebilir.
- **Alternatives**: Buy-box kazananının içeriği — fiyat değişince içerik zıplar; red. Son yazan — sıra-bağımlı; red.