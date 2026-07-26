# Research: Tedarikçi Feed'i = Stoğun Tek Otoritesi

Spec'te `[NEEDS CLARIFICATION]` yoktu (kritik karar spec öncesi kullanıcıyla netleşti).
Bu dosya tasarım kararlarını ve reddedilen alternatifleri kayda geçirir.

## D1 — Feed OnHand'i ezer (mutlak set), create + update'te

- **Karar**: StockWrite her teslimde (yeni ya da değişmiş kayıt) `set_stock(ProductId,
  StockQuantity)` ile OnHand'i **mutlak** feed değerine ayarlar.
- **Gerekçe**: Kullanıcı kararı — feed stoğun tek otoritesi. Snapshot-diff kapısı zaten
  yalnız değişen kaydı yayınladığından "her pull'da ez" pratikte "tedarikçi değişince ez"e
  iner. Create/update ayrımına (RecordJob'da Action) gerek kalmaz → daha az hareketli parça.
- **Reddedilen**: "Yalnız ilk kez (seed, yoksa-ekle)". Kullanıcı re-sync istedi (US2);
  seed-only feed güncellemelerini yansıtmazdı.
- **Sonuç/risk**: Yerel Commit düşüşü, sonraki tedarikçi değişikliğinde OnHand'de silinir.
  Checkout `AvailableAt` (0'a kırpar) + `IsOversoldAt` ile korunur (Stock aggregate hazır).

## D2 — ProductCreatedEvent komple kaldırılır (Quantity'yi kırpmak yerine)

- **Karar**: `ProductCreatedEvent` + `ProductStockInfo` + `RabbitMqConstants.ProductCreated`
  tamamen silinir.
- **Gerekçe**: Bu event YALNIZ stok adedini Catalog'dan Stock'a taşımak için vardı. Tek
  üretici `CreateProduct`, tek tüketici Stock'un seed handler'ı. Storefront `ProductChangedEvent`
  kullanır (create'te de yayılır). Seed kalkınca event'in tüketicisi kalmaz → ölü kontrat.
- **Reddedilen**: "Event'i tut, yalnız Quantity alanını çıkar." YAGNI — tüketicisiz boş
  kontrat bırakmak; BC izolasyonu (İlke I) tam temizlikle daha net.
- **Doğrulama**: `grep ProductCreated` → yalnız Shared + Catalog + Stock; Storefront yok.

## D3 — set_stock MCP tool + SetStock command KORUNUR; yalnız REST /set kalkar

- **Karar**: StockWrite mevcut `set_stock` MCP tool'unu çağırır (yeni tool yazılmaz).
  `SetStock` command + handler kalır. Yalnız manuel `SetStockCommandEndpoint` (REST PUT
  `/set`) silinir.
- **Gerekçe**: Kullanıcı "manuel set_stock kalksın" dedi = operatör/dışarıdan elle set
  yolu. Tool zaten 005'te ingestion için yazılmış (mutlak, yoksa-0'dan-açar upsert) — tam
  ihtiyaç. Storefront'u besleyen `StockChangedEvent` yayınını da SetStock handler'ı yapar.
- **Reddedilen**: `set_stock`'u da silip yeni `seed_stock` yazmak. Gereksiz — mevcut mutlak
  semantik D1 ile birebir uyumlu.
- **Not**: MCP tool teknik olarak herhangi bir MCP client'a açık; pratikte tek yazan
  IngestionAgent (ChatAgent stoğa yazmaz). "Tek yazım yolu" garantisi topolojiktir.

## D4 — Workflow sırası: Catalog → Stock → Discount

- **Karar**: StockWrite, Catalog ile Discount arasına eklenir (007'deki orijinal sıra).
- **Gerekçe**: StockWrite `ProductId`'ye muhtaç; onu CatalogWrite doldurur. Discount da
  ProductId'ye muhtaç, sonda kalabilir. Ara executor short-circuit'lidir: `job.Failure`
  doluysa dokunmadan geçirir (son executor Discount `Completed`'ı işaretler).
- **Reddedilen**: Catalog → Discount → Stock. Fark yok; 007 sırasını korumak daha az sürpriz.

## D5 — Hata semantiği: mevcut ingestion modeli

- **Karar**: StockWrite başarısızlığı `job.Failure = "STOCK_WRITE_FAILED: ..."` yazar →
  `SupplierSnapshotHandler` `IngestionWriteException` fırlatır → Wolverine kademeli retry
  → tükenince DLQ. CatalogWrite/DiscountWrite ile birebir aynı desen.
- **Gerekçe**: Tutarlılık; 013'te doğrulanmış at-least-once + retry/DLQ yolu korunur.
- **İdempotency**: Mutlak set → çift-teslim aynı değeri yazar (SC-005). Retry güvenli.

## D6 — Catalog manuel create'in stok alanı düşer (kabul edilen sonuç)

- **Karar**: `CreateProductCommand`/`upsert_product`'tan `InitialStock` çıkınca, feed-dışı
  (manuel) oluşturulan ürün stok almaz (feed StockWrite yalnız ingestion mesajından çalışır).
- **Gerekçe**: Kullanıcı "yalnız tedarikçi ürünleri" dedi; manuel oluşturma kapsam dışı.
- **Sonuç**: Manuel Create Product UI'si stok alanını kaybeder; manuel ürün 0 stokla kalır.
  Kabul edilen (spec Assumptions). İleride manuel akış gerekirse ayrı feature.