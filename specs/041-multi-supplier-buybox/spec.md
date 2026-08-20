# Feature Specification: Multi-Supplier Dropship — Procurement BC (Havuz + Buy-Box)

**Feature Branch**: `041-multi-supplier-buybox`

**Created**: 2026-08-19

**Status**: Draft

**Input**: User description: "Çok-tedarikçi dropship: tüm tedarikçi ürünleri Procurement havuzuna alınır, barkodla
gruplanır; eksik veriler agent (AI) ile tamamlanır; eksiksiz kanonik ürün ilgili servislere publish edilir. Otomatik
buy-box (stoklu en ucuz) fiyat + stok belirler. Kaynak: Obsidian 12'lik extract listesinin 7. maddesi (Catalog-Variants)
tartışmasından evrilen dropship yönü; varyant ertelendi."

## Bağlam

- Kademe: **Tam** — yeni BC (Procurement), yeni aggregate'ler, yeni integration event'ler, servis söküm/birleştirme.
- Soy: 12'lik extract sıralamasının 7. maddesi (varyant) brainstorming'de dropship'e evrildi; varyant ERTELENDİ.
  Barkod = ürün mutlak kural olduğundan varyant ileride aile-gruplama olarak kırılmasız biner.
- 040 ön koşulu tamam: `Gtin` Catalog'da hazır ve boş; bu feature feed'e barkodu ekler ve doldurur.
- Onay ekranı YOK — kontrol otomatik buy-box + guard'lar; barkod kontrolü yalnız EŞLEŞMEdir (geçerlilik doğrulaması yok).
- Saga YOK — idempotent upsert + sınırlı retry + DLQ yeterlidir (telafi semantiği yok).
- PO/webhook (dropship ileri/geri bacak), fuzzy/barkodsuz eşleme ve varyant 041 SONRASI dilimlere ertelendi.

## Clarifications

### Session 2026-08-19

- Q: Kazanan kalmayınca ürün vitrinde nasıl davranır? → A: Vitrinde KALIR; OnHand=0, satın alınamaz. Unpublish yok.
- Q: Brand/Category feed'den mi doğsun, baştan mı seed'lensin? → A: Yol 2 — kanonik Category>SubCategory ağacı seed'lenir;
  tedarikçi başına statik kategori-eşleme tablosu; mock feed'ler bilerek farklı taksonomi adları üretir.
- Q: Tedarikçi işleme sırası farklı olabilir mi? → A: Evet; buy-box sıradan bağımsız. Supplier kayıtları seed'lenir
  (statik kimlik) — eşitlik tie-break'i deterministik olur.
- Q: Mock'ta varyant ("Sarı XL") ve görsel olacak mı? → A: HAYIR; varyantsız ürün türleri, görselsiz. Barkod = ürün.
- Q: İçerik kuralı first-writer-wins mi? → A: HAYIR. Tüm satırlar havuza alınır, barkodla gruplanır; kanonik içerik
  havuzda birleştirilir, eksik alanları enrich agent (AI) tamamlar; eksiksiz kayıt publish edilir.
- Q: İşlenen havuz verisi ne olur? → A: SİLİNMEZ; durum makinesi (Pending→Enriched→Published) + içerik hash'i ile kalır.
  Değişmeyen satır yeniden işlenmez; feed'den düşen barkod Delisted işaretlenir.
- Q: Havuz nerede yaşar? → A: AYRI BC değil ayrı servisler; havuz + Supplier/SupplierOffer + buy-box + enrich TEK yeni
  BC'de: **Procurement**. Supplier.Gateway'in işi Procurement'a taşınır; IngestionAgent tamamen sökülür.
- Q: Ölçü (ProductDimensions) test verisine girsin mi? → A: EVET; feed satırı ölçü taşır, 040'ın pasif alanı dolar.
  Eksik ölçüyü AI uydurmaz (fiziksel gerçek); boş varsayılan yayını bloklamaz.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Vitrin benzersiz ürünü en iyi fiyatla gösterir (Priority: P1)

İki tedarikçi feed'i Procurement havuzuna alınır; müşteri vitrinde tedarikçi satırlarını değil benzersiz barkodları
görür. Çakışan üründe fiyat, stoklu en ucuz tedarikçinin fiyatıdır (buy-box); müşteri tedarikçiyi hissetmez.

**Why this priority**: Feature'ın kalbi — çok-tedarikçi rekabeti tek vitrin ürününe iner; katalog şişmez, fiyat en iyidir.

**Independent Test**: İki mock feed ingest edilir; vitrinde benzersiz barkod sayısı görünür, çakışan ürünün fiyatı doğrulanır.

**Acceptance Scenarios**:

1. **Given** boş sistem, **When** A (1800) ve B (1700) feed'leri işlenir, **Then** vitrinde 3000 benzersiz ürün görünür.
2. **Given** iki tedarikçide de stoklu çakışan barkod, **When** vitrine bakılır, **Then** fiyat en ucuz offer'ın fiyatıdır.
3. **Given** çakışan barkodda eşit fiyat, **When** buy-box seçilir, **Then** düşük SupplierId deterministik kazanır.
4. **Given** feed'ler ters sırada işlenir, **When** havuz ve buy-box tamamlanır, **Then** sonuç sıradan bağımsız aynıdır.

---

### User Story 2 - Buy-box değişimi vitrine yansır (Priority: P2)

Tedarikçi fiyat/stok değiştirir; sonraki feed çekiminde kazanan yeniden seçilir. Kazanan stoksuz kalırsa
sonraki en ucuz stoklu offer kazanır; hiç stoklu offer kalmazsa ürün vitrinde kalır ama satın alınamaz.

**Why this priority**: Buy-box'ın canlı değeri — fiyat/stok rekabeti sürekli işler; oversell ve bayat fiyat engellenir.

**Independent Test**: Feed'de fiyat/stok değişimi simüle edilir; vitrin fiyatı, stok ve satın alınabilirlik doğrulanır.

**Acceptance Scenarios**:

1. **Given** kazanan offer, **When** feed'de rakip daha ucuza iner, **Then** yeni kazananın fiyat+stoğu vitrine yansır.
2. **Given** kazanan offer, **When** feed'de stoğu 0 olur, **Then** sonraki en ucuz stoklu offer kazanır.
3. **Given** tüm offer'lar stoksuz, **When** buy-box değerlendirilir, **Then** ürün vitrinde kalır, stok 0, satın alınamaz.
4. **Given** değişmeyen feed, **When** aynı feed tekrar işlenir, **Then** hiçbir yayın/yeniden işleme olmaz (hash aynı).

---

### User Story 3 - Havuz toplar, eksik veriyi agent tamamlar, eksiksizi yayınlar (Priority: P2)

Tüm tedarikçi satırları ham olarak havuza düşer ve barkodla gruplanır. Tedarikçi kategorisi statik eşleme tablosuyla
kanonik taksonomiye çevrilir. Eksik alanlı kayıtları enrich agent (AI) tamamlar; yalnız EKSİKSİZ kanonik ürün
Catalog'a yayınlanır. Yapısal (tam) kayıtlar AI'sız, ucuz yoldan geçer.

**Why this priority**: Havuz + enrich, kanonik içeriğin kaynağıdır; maliyet hedefi = AI yalnız eksik kayıtta çalışır.

**Independent Test**: Mock'taki bilinçli eksik kayıtlar enrich'ten geçip yayınlanır; tam kayıtların yolunda AI çağrısı
olmadığı loglarla doğrulanır.

**Acceptance Scenarios**:

1. **Given** tam feed satırı, **When** havuz işler, **Then** AI çağrısı OLMADAN kanonik kayıt yayınlanır.
2. **Given** eksik alanlı satır (örn. açıklama/kategori yok), **When** enrich koşar, **Then** agent eksikleri tamamlar
   ve kayıt yayınlanır; sonuç havuzda saklanır (aynı içerik için AI tekrar çalışmaz).
3. **Given** tedarikçiye özgü kategori adı, **When** eşleme tablosu uygulanır, **Then** ürün kanonik kategoriye bağlanır.
4. **Given** eşlenemeyen VE enrich'in de çözemediği kategori, **When** yol tükenir, **Then** kayıt yayınlanmaz, DLQ/log'a düşer.
5. **Given** işlenemeyen kayıt, **When** retry'lar tükenir, **Then** mesaj içeriğiyle DLQ'ya düşer, kalan akış sürer.
6. **Given** barkodsuz satır, **When** havuz alımı koşar, **Then** satır reddedilir ve loglanır (AI barkod ÜRETMEZ).

---

### Edge Cases

- Aynı barkodda iki tedarikçi farklı dolu değer verir (örn. iki farklı ad): alan bazında deterministik birleşme —
  düşük SupplierId'nin dolu değeri öncelikli; eksik alan diğerinden tamamlanır; hâlâ eksikse AI.
- Buy-box kararı geldiğinde barkod Catalog'da henüz yok (sıralama yarışı): kayıp yaşanmaz, akış nihai tutarlı kalır.
- Sipariş stoğu bitirir: buy-box gecikmesi v1'de KABUL — sonraki feed çekiminde düzelir (ileride StockDepleted event).
- Kazanan değişince Stock, KAZANANIN stoğunu mutlak yazar (toplam değil); önceki kazananın kalıntısı kalmaz.
- Yalnız tek tedarikçide olan barkod: buy-box tek offer'la çalışır (stoklu ise satışta, değilse stok 0).
- Feed'den düşen barkod: havuzda Delisted işaretlenir; o tedarikçinin offer'ı yarıştan çıkar; ürün silinmez.
- Feed çekimi kısmen başarısız: işlenen kayıtlar geçerli kalır; başarısızlar retry/DLQ yoluna gider.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Supplier.Api iki feed ucu sunar (`supplier-a`, `supplier-b`); veri deterministik sabit-seed ile üretilir.
- **FR-002**: Mock hacim: A=1800, B=1700 satır; 500 çakışan barkod; toplam 3000 benzersiz barkod. Çakışanlarda fiyat
  kimi A kimi B ucuz, kimi eşit; kimi offer stok 0; ad/açıklama hafif farklı.
- **FR-003**: Mock içerik: varyantsız ürün türleri (renk/beden YOK), görsel YOK; satır = barkod (zorunlu), ad, açıklama,
  marka, tedarikçiye özgü kategori adı, tedarikçi SKU'su, fiyat, stok, ölçü (ağırlık+en/boy/yükseklik — ürün türüne
  uygun deterministik değerler). Bir kısım satır bilinçli EKSİK alanlı üretilir.
- **FR-004**: Mock'ta iki tedarikçi bilerek FARKLI taksonomi adları kullanır (eşleme katmanı canlı test edilir).
- **FR-005**: Yeni **Procurement BC** açılır: tedarikçi feed'lerini feed-başına adapter'la çeker (Supplier.Gateway'in
  işi buraya taşınır), ham satırları havuza yazar, barkodla gruplar, kanonikleştirir, buy-box hesaplar, yayınlar.
- **FR-006**: Havuz kalıcıdır: ham satır (tedarikçi × barkod) upsert edilir; kayıt durum (Pending→Enriched→Published),
  içerik hash'i ve yayın zamanı taşır. Hash değişmeyen satır yeniden işlenmez; silme yerine Delisted işareti kullanılır.
- **FR-007**: Supplier kayıtları ve kanonik Category>SubCategory ağacı (iki seviye) seed'lenir; tedarikçi başına statik
  kategori-eşleme tablosu tedarikçi adını kanonik kategoriye çevirir. Yeni kategori feed'den DOĞMAZ.
- **FR-008**: Kanonik içerik birleştirme deterministiktir: alan bazında düşük SupplierId'nin dolu değeri öncelikli;
  eksik alan diğer tedarikçiden tamamlanır. Sonuç işleme sırasından bağımsızdır.
- **FR-009**: Hâlâ eksik kalan alanları Procurement içindeki enrich agent (AI) tamamlar (kategori seçimi kanonik
  listeden). AI yalnız eksik kayıtta çalışır; sonuç havuzda saklanır (aynı içerik için tekrar çağrılmaz).
- **FR-010**: AI, kimlik anahtarı (barkod) ve FİZİKSEL gerçekleri (ölçü/ağırlık) ASLA üretmez/uydurmaz; barkodsuz
  satır havuz girişinde reddedilir + loglanır. Eksik ölçü boş varsayılanla yaşar, yayını BLOKLAMAZ (eksiksizlik
  şartı içerik alanları içindir: ad/açıklama/kategori).
- **FR-011**: Yalnız EKSİKSİZ kanonik ürün Catalog'a yayınlanır; Catalog kendi modeline yazar (Gtin=barkod, Sku ve
  ölçü kanonik birleşimden — 040 `ProductDimensions` bu feature ile dolar). Eksik/eşlenemeyen kayıt yayınlanmaz;
  retry sonrası DLQ/log'a düşer.
- **FR-012**: SupplierOffer = (barkod, tedarikçi) → fiyat + stok; feed ile mutlak güncellenir; yazımlar idempotent.
- **FR-013**: Buy-box kuralı saf domain mantığıdır (test-first): stok>0 olanların en ucuzu kazanır; eşitlikte düşük
  SupplierId; kazanan stoksuz kalırsa sonraki en ucuz; hiç aday yoksa kazanan yok (stok 0, satın alınamaz).
- **FR-014**: Procurement, kazanan değiştiğinde `BuyBoxChanged {Barcode, SupplierId, Price, Stock}` yayınlar;
  değişim yoksa yayın yapılmaz.
- **FR-015**: Catalog `BuyBoxChanged` tüketir: ürünü Gtin ile bulur, fiyatı günceller. Kazanansız durumda ürün
  vitrinde kalır (unpublish YOK); satın alınamazlık stok 0 ile sağlanır.
- **FR-016**: Catalog, barkod-ürün eşleşmesi kurulduğunda `ProductLinked {Barcode, ProductId}` yayınlar; Stock bununla
  eşleme kurar ve OnHand'i KAZANAN offer'ın stoğuyla mutlak yazar (offer'ların toplamı değil).
- **FR-017**: IngestionAgent projesi ve 015 LLM yazıcı zinciri tamamen sökülür. OpenAI kullanımı yalnız ChatAgent,
  Storefront embedding ve Procurement enrich agent'ındadır; tam-kayıt yapısal yolda AI çağrısı SIFIRDIR.
- **FR-018**: Akışta saga kullanılmaz; dayanıklılık idempotent upsert + sınırlı retry + DLQ ile sağlanır.
- **FR-019**: Onay ekranı/insan onayı yoktur; kontrol otomatik buy-box + guard'lardır (barkod zorunlu, kategori
  eşleme, eksiksizlik şartı).

### Key Entities

- **Supplier** (Procurement, seed): statik kimlikli tedarikçi; tie-break ve eşleme tablolarının çapası.
- **Havuz ham satırı** (Procurement): (tedarikçi × barkod) ham feed verisi + durum + hash; kalıcı, upsert'lenir.
- **Kanonik havuz kaydı** (Procurement): barkod başına birleştirilmiş + enrich'lenmiş içerik; publish kaynağı.
- **SupplierOffer** (Procurement): (barkod, SupplierId) → fiyat + stok; buy-box adayı.
- **Kanonik taksonomi + eşleme tablosu** (Procurement, seed): Category>SubCategory ağacı; tedarikçi-adı → kanonik.
- **Product** (Catalog, mevcut): Gtin=barkod; içerik Procurement yayınından; fiyat/stok buy-box'tan.
- **BuyBoxChanged**: {Barcode, SupplierId, Price, Stock} — Procurement → Catalog/Stock sözleşmesi.
- **ProductLinked**: {Barcode, ProductId} — Catalog → Stock eşleme sözleşmesi.
- **Eksiksiz-ürün yayın kontratı**: Procurement → Catalog kanonik ürün event'i (adı /plan'da netleşir).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Çözüm derlenir; mevcut ve yeni tüm testler yeşildir; buy-box + birleşme kuralları test-first birim testlidir.
- **SC-002**: Canlı: iki feed ingest sonrası vitrinde tam 3000 benzersiz ürün görünür (tedarikçi satırı değil barkod).
- **SC-003**: Canlı: çakışan barkodlu örnek üründe vitrin fiyatı stoklu en ucuz offer'la birebir eşleşir.
- **SC-004**: Canlı: kazanan değişimi (fiyat/stok) sonraki feed çekiminde vitrine yansır; kazanansız ürün vitrinde
  stok 0 görünür ve satın alınamaz.
- **SC-005**: Canlı: bilinçli eksik mock kayıtları enrich'ten geçip yayınlanır; tam kayıtların yolunda AI çağrısı
  olmadığı loglarla doğrulanır.
- **SC-006**: Canlı: farklı taksonomi adlı iki feed tek kanonik ağaçta birleşir; eşlenemeyen kayıt DLQ/log'da görünür.
- **SC-007**: Aynı feed'in tekrar işlenmesi hiçbir yayın/yeniden işleme üretmez (hash idempotency canlı doğrulanır).
- **SC-008**: Feed işleme sırası değiştirilse de (A önce / B önce) kanonik içerik ve buy-box sonucu AYNI kalır.

## Assumptions

- Ortam dev'dir; veri migration derdi yok — DB sıfırlama + feed replay serbesttir (ürünler yalnız feed'den).
- Supplier.Api dış dünya maketidir; iki feed ucu da onun üstünde yaşar (gerçek tedarikçi entegrasyonu yok).
- Tek para birimi (TRY) sürer; offer fiyatı Catalog `Money` VO'suna sayısal akar. Fiyat ~50–5000 TL, stok 0–100 bandı.
- Eksik üretilen mock satır oranı ~%10 (açıklama ve/veya kategori boş) — enrich yolunu tetiklemeye yeter.
- Basket/Order/checkout akışı değişmez; Stock OnHand güncel kaldığı sürece mevcut rezervasyon/commit düzeni çalışır.
- Supplier.Gateway servisi ve `supplierGatewayDb` bu feature ile emekli olur; yerini Procurement BC + kendi DB'si alır.
- PO (tedarikçiye sipariş), kargo webhook'u, fuzzy/barkodsuz eşleme, varyant/aile gruplama sonraki dilimlerdir.
- MPN boş kalır; görsel akışı bu feature'da yok (File.Api düzeni değişmez).