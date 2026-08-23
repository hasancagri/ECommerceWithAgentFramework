# Phase 0 — Research & Decisions

Feature: Heterogeneous Supplier Feed (ACL) + Buy-box Teardown. Spec belirsizliği yok; kararlar tasarım
konuşmasında alındı, burada gerekçelerle sabitlenir.

## D1 — Heterojen feed topolojisi (Supplier.Api)

- **Karar**: Tek Supplier.Api process; her tedarikçi AYRI literal route + AYRI response modeli + AYRI
  şekilli dataset. `GET v1/feeds/supplier-a` → A-şekli; `GET v1/feeds/supplier-b` → B-şekli. **`advance`
  POST + rev makinesi + `Revisions` sözlüğü SİLİNİR**; tedarikçi başına TEK dataset dosyası
  (`supplier-a.json`, `supplier-b.json`), istek anında okunur (canlı düzenleme yansır).
- **Rationale**: Salt path-param (bugün) tek `SupplierFeedRow` deserialize/serialize ettiğinden şekil
  sabittir — heterojenlik ancak per-tedarikçi typed model + handler ile doğar. 2 tedarikçi = 2 literal
  uç (düz kod, mock için kabul). Path-traversal guard literal route'ta gereksizleşir. `advance` fazlalık:
  dosya istek anında okunduğundan feed değişimi dosyayı elle düzenleyerek simüle edilir (kullanıcı kararı).
- **Alternatifler**: (a) Ham JSON pass-through — tip güvenliği/örneklik kaybı, red. (b) Ayrı Aspire
  servisi per tedarikçi — kullanıcı kapsam-dışı bıraktı (deploy topoloji ağırlığı), red.

## D2 — Procurement ACL adapter'ı

- **Karar**: `ISupplierFeedAdapter { string SupplierCode; Task<IReadOnlyList<SupplierFeedRowDto>>
  FetchAsync(ct); }`. İki impl: `SupplierAFeedAdapter`, `SupplierBFeedAdapter`; her biri KENDİ ham DTO'sunu
  (A/B feed şekli) HTTP GET'le çeker + nötr `SupplierFeedRowDto`'ya map eder. `PullSupplierFeed` code'a
  göre doğru adapter'ı seçer (inject `IEnumerable<ISupplierFeedAdapter>` → `SupplierCode` eşleşmesi).
- **Rationale**: ACL = İlke I'in dış-sistem sınırı; yabancı şekil iç modele sızmaz. `SupplierFeedRowDto`
  nötr ACL hedefi olur → `PullSupplierFeed`'in DTO→`ListingRow` map'i DEĞİŞMEZ (churn minimum). Adapter'lar
  Scrutor marker'ıyla otomatik kaydolur.
- **Alternatifler**: (a) Tek generic adapter + config-map alanları — heterojen tip güvenliğini kaybeder,
  red. (b) Adapter doğrudan `ListingRow` üretir — kategori/attribute çözümü handler'da; sınırı bulanıklaştırır,
  red (nötr DTO'da dur).

## D3 — Per-tedarikçi adres yapılandırması

- **Karar**: `SupplierFeedEndpointsOptions { Dictionary<string,string> Paths }` (code→relatif path, ör.
  `"supplier-a": "/v1/feeds/supplier-a"`), appsettings'ten `BindConfiguration().ValidateOnStart()`. Base
  host service-discovery `services:supplier-api:http:0`'tan (Options istisnası — dinamik-key lookup).
- **Rationale**: FR-001 "adres yapılandırmadan". Konvansiyon: `IConfiguration` doğrudan-okuma yasağı;
  section → POCO. Service-discovery dinamik-key istisnası zaten mevcut `SupplierFeedClient`'te kullanılıyor.
- **Alternatifler**: Adresi `Supplier` aggregate seed'ine koymak — domain'e altyapı-adresi sızdırır, red.

## D4 — Buy-box söküm: PoolProduct sadeleşmesi

- **Karar**: `_listings` (List) → tek `SupplierListing? Listing` (barkod-başı tek tedarikçi). SİL:
  `EvaluateBuyBox`, `BuyBoxDecision`, `PublishedBuyBox`, kazanansız/tiebreak mantığı, `SupplierListing.
  SupplierPriority`. `RebuildCanonical` tek listing + enrich overlay'den kanonik kurar (OrderBy/priority-
  merge/cross-listing spec-grouping KALKAR; specs tek listing'ten). Delist → listing delisted → offer
  stok 0, fiyat son-bilinen.
- **Rationale**: Barkod tekil = tek tedarikçi = çoklu-offer ölü. Kullanıcı "tam söküm" istedi (uykuda
  değil). Merge artık tek kaynaktan → deterministik ve daha basit.
- **Alternatifler**: Koleksiyonu tutup max-1 zorlamak — ölü karmaşıklık kalır, kullanıcı reddetti.

## D5 — Fiyat/stok tek kanal: CanonicalProductUpserted

- **Karar**: `BuyBoxChanged` record SİLİNİR. `CanonicalProductUpserted` (şekli aynen — zaten Price+Stock
  taşıyor) TEK güncelleme kanalı olur; **içerik-hash VEYA fiyat VEYA stok** değişince yayınlanır.
  `TryTakePublish` `PublishedPrice`+`PublishedStock` izler; karar tek `PublishCanonical` bool'a iner.
- **Downstream**: Catalog `Handle(BuyBoxChanged)` SİL — `Handle(CanonicalProductUpserted)` zaten fiyatı
  yazıyor (idempotent upsert, fiyat/stok-only olayda da güvenli). Stock `Handle(BuyBoxChanged)` → yeni
  `Handle(CanonicalProductUpserted)`: BarcodeLink lookup + `SetQuantity(evt.Stock)` + `StockChangedEvent`
  (aynı gövde). Stock kuyruğu `CanonicalProductUpserted`'a YENİ binding kurar (tüketici bağlar).
- **Yarış edge'i (R4 korunur)**: Yeni üründe ilk `CanonicalProductUpserted` Stock'a gelir ama BarcodeLink
  henüz yok (onu Catalog `ProductLinked` ile sonra kurar) → Stock atlar; ilk stok `ProductLinked.
  InitialStock`'tan. Sonraki `CanonicalProductUpserted`'lar (stok değişimi) link'i bulur.
- **Rationale**: Kullanıcı "fiyat/stok tek kanonik kanaldan aksın" dedi. `ProductLinked` ilk-değer +
  yarış-kapısı rolünü korur (değişmez).
- **Alternatifler**: Fiyat/stoku içerik-hash'e katmak — hash semantiği bulanır (içerik≠fiyat), red;
  ayrık izleme (`PublishedPrice/Stock`) daha nettir.

## D6 — Priority alanının akıbeti

- **Karar**: `Supplier.Priority` seed alanı KALIR (zararsız; FeedPullJob pull-sırası için kullanır) ama
  merge/seçimde KULLANILMAZ. `SupplierListing.SupplierPriority` SİLİNİR (yalnız merge/tiebreak'i besliyordu).
  `UpsertListing` imzasından `supplierPriority` param düşer.
- **Rationale**: FR-025 — priority'nin tek domain anlamı merge-sırasıydı; çoklu-offer gidince anlamsız.
  Seed alanını silmek gereksiz göç; pull-sıra için bırakmak ucuz.

## D7 — Tek-gate idempotency (ListingChange söküm)

- **Karar**: Listing-düzeyi değişim-tespiti SİLİNİR — `ListingChange` enum, `SupplierListing.ContentHash`,
  `UpsertListing`'in hash-diff dalları, handler'ın Unchanged/Changed sayaç mantığı. İdempotency TEK
  noktada kalır: `TryTakePublish` → `PublishedContentHash`/`PublishedPrice`/`PublishedStock`. Handler
  sadeleşir: her satır upsert → RebuildCanonical → PublishPoolProduct; yayın kararı tek yerde verilir.
- **Rationale**: İki gate vardı (listing erken-çıkışı + publish-gate). Downstream sessizliğini asıl
  publish-gate sağlar; listing-gate yalnız CPU-tasarrufu erken-çıkışıydı. Kullanıcı seremoni-sadeleştirme
  istedi; tek-gate downstream davranışını KORUR (değişmemiş pull sıfır event), yalnız her satır için
  rebuild+publish-invoke CPU'su artar (mock ölçeğinde önemsiz).
- **Alternatif (red)**: Publish-gate'i de silmek → her pull tüm event seti çıkar (downstream idempotent
  upsert yutar ama israf). Kullanıcı önerimle tek-gate'te durdu.

## Açık bırakılan (KAPSAM DIŞI, kayıtlı)

- **Barkod tekillik-guard** implementasyonu — ayrı açık araştırma (Obsidian
  `supplier-realism-barcode-competition-open-question`). Bu feature tekilliği yalnız ELLE varsayar.
- **Ağ/auth gerçekçiliği** (API key, imzalı feed) — mock anonim kalır.