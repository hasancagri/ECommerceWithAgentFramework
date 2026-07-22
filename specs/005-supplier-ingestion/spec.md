# Feature Specification: Tedarikçi Entegrasyonu (Supplier Ingestion)

**Feature Branch**: `005-supplier-ingestion`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "Tedarikçi entegrasyonu: sistemi gerçek bir ürün sağlayıcı senaryosuna yaklaştırmak için
tedarikçi simülatörü + MAF Workflows tabanlı ingestion uygulaması; staging DB, adapter'lar, deterministik idempotency,
agent başına tek MCP; Category ve kesişen kataloglar kapsam dışı."

**Artefakt Kademesi**: Tam — yeni projeler, yeni veritabanları, servislere MCP ile yazma ve yeni feed sözleşmeleri var.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Feed'lerden ürün aktarımı (Priority: P1)

Operatör bir aktarım (ingestion run) tetikler. Sistem üç tedarikçinin feed'ini çeker.
Her kayıt ortak ara modele çevrilir, ara depoda saklanır ve ilgili servislere ürün + stok olarak yazılır.

**Why this priority**: Feature'ın varlık sebebi; ürünler artık elle/seed ile değil, tedarikçi verisinden gelir.

**Independent Test**: Simülatörde 3 veri seti hazırken bir run tetiklenir; tüm ürünler katalog ve stokta görünür.

**Acceptance Scenarios**:

1. **Given** üç tedarikçide toplam N ürün, **When** run tetiklenir, **Then** katalogda N yeni ürün oluşur.
2. **Given** feed'de stok adedi olan kayıt, **When** aktarım biter, **Then** ürünün stok kaydı bu adedi gösterir.
3. **Given** feed'deki marka metni ("Apple" gibi), **When** kayıt işlenir, **Then** ürün sistemin marka tanımına eşlenmiştir.
4. **Given** run tamamlanır, **When** özet istenir, **Then** yeni/güncellenen/atlanan/hatalı sayıları raporlanır.
5. **Given** indirim yüzdesi taşıyan kayıt, **When** aktarım biter, **Then** üründe bu oranda ürün indirimi tanımlıdır.

---

### User Story 2 - Tekrarlanan aktarımda idempotency (Priority: P2)

Operatör aynı feed'leri ikinci kez çeker. Değişmemiş kayıtlar hiç işlenmez; sistemde mükerrer ürün oluşmaz.

**Why this priority**: Kullanıcının temel endişesi; idempotency olmadan feature üretimde kullanılamaz.

**Independent Test**: Aynı run iki kez tetiklenir; ikinci run'da domain'e sıfır yazma olur, atlanan sayısı toplamı eşitler.

**Acceptance Scenarios**:

1. **Given** başarıyla işlenmiş feed, **When** aynı feed tekrar çekilir, **Then** hiçbir kayıt yeniden işlenmez.
2. **Given** işlenmiş kayıt, **When** aynı içerik tekrar gelir, **Then** katalog/stokta hiçbir değişiklik olmaz.
3. **Given** "işlenecek mi" kararı, **When** run çalışır, **Then** karar her zaman deterministik koddadır, agent'a sorulmaz.

---

### User Story 3 - Değişen kayıtların güncellenmesi (Priority: P3)

Tedarikçi bir ürünün fiyatını/bilgisini değiştirir. Sonraki aktarımda yalnızca değişen kayıtlar güncelleme olarak işlenir.

**Why this priority**: Full feed dünyasında sürekli senkron bu mekanizmayla sağlanır; P1+P2 üstüne doğal katman.

**Independent Test**: Bir kaydın fiyatı değiştirilip run tekrarlanır; yalnız o ürün güncellenir, kalanlar atlanır.

**Acceptance Scenarios**:

1. **Given** içeriği değişen tek kayıt, **When** run tetiklenir, **Then** yalnız o ürün güncellenir, gerisi atlanır.
2. **Given** güncellenen kayıt, **When** işlem biter, **Then** ara depodaki içerik izi (hash) yeni içeriği yansıtır.
3. **Given** indirimi feed'den kaldırılan ürün, **When** run tetiklenir, **Then** üründeki indirim kaldırılır.

---

### User Story 4 - Hatalı kayıtların izolasyonu (Priority: P4)

Feed'de bozuk/eksik kayıtlar vardır. Bunlar akışı durdurmaz; nedenleriyle işaretlenir ve sonradan incelenebilir.

**Why this priority**: Dış veri her zaman kirlidir; izolasyon olmadan tek bozuk kayıt tüm aktarımı düşürür.

**Independent Test**: Veri setine bilinçli bozuk kayıt eklenir; run biter, sağlam kayıtlar işlenir, bozuk kayıt nedeniyle listelenir.

**Acceptance Scenarios**:

1. **Given** feed'de bozuk kayıt, **When** run çalışır, **Then** kalan kayıtlar işlenir, run başarıyla tamamlanır.
2. **Given** hatalı işaretli kayıt, **When** operatör inceler, **Then** ham veri ve hata nedeni görüntülenebilir.
3. **Given** hatası giderilmiş kayıt, **When** run tekrar tetiklenir, **Then** kayıt yeniden işlenmeyi dener.

---

### Edge Cases

- Tedarikçi feed'i erişilemezse: o tedarikçi atlanır, run diğerleriyle devam eder; durum raporlanır.
- Feed boş dönerse: hata değildir; sıfır kayıt işlenir, özet bunu gösterir (silme/delist tetiklemez).
- Aynı feed içinde mükerrer harici kimlik: ilk kayıt esas alınır, sonrakiler hatalı işaretlenir.
- Marka metni sistemin marka tanımlarına eşlenemezse: kayıt hatalı işaretlenir, nedeni kaydedilir.
- İndirim yüzdesi geçersizse (≤ 0 veya > 100): kayıt hatalı işaretlenir; ürün/stok yazımı yapılmaz.
- Domain servisine yazma başarısız olursa: kayıt hatalı işaretlenir; sonraki run'da yeniden denenir.
- Run sürerken ikinci tetikleme gelirse: reddedilir veya kuyruklanır; aynı anda iki run çalışmaz.

## Requirements *(mandatory)*

### Functional Requirements

#### Tedarikçi simülatörü

- **FR-001**: Sistem, üç tedarikçi kimliğini tek bir simülatör servisiyle sunmalıdır; her tedarikçinin feed'i ayrı uçtan çekilir.
- **FR-002**: Üç feed üç farklı biçimde yayınlanmalıdır: A = JSON API, B = CSV dump, C = XML feed.
- **FR-003**: Her feed, tedarikçinin TÜM ürünlerini içeren tam bir anlık görüntü (full feed) olmalıdır; delta/artımlı feed yoktur.
- **FR-004**: Veri setleri kullanıcı tarafından hazırlanır ve simülatörün kendi veritabanına açılışta yüklenir (seed).
- **FR-005**: Kataloglar marka bazında ayrık olmalıdır: A = Apple/Samsung/Sony, B = Nike/Adidas, C = Lenovo/Dell/Hp/Asus/Xiaomi.
- **FR-006**: Feed kaydı en az şunları taşır: harici kimlik, ad, açıklama, ham marka, fiyat, stok adedi; opsiyonel indirim kodu + yüzdesi.

#### Aktarım (ingestion)

- **FR-007**: Operatör bir aktarımı istek üzerine (on-demand) tetikleyebilmelidir.
- **FR-008**: Sistem üç feed'i de çekmeli; her tedarikçi için o kaynağa özgü bir çevirici (adapter) ham kaydı ortak ara modele dönüştürmelidir.
- **FR-009**: Her çekilen kayıt ara depoda saklanmalıdır: tedarikçi + harici kimlik anahtarı, ham veri, içerik izi (hash), durum.
- **FR-010**: Ham veri (RawPayload) geldiği haliyle saklanmalıdır; inceleme ve tedarikçiye gitmeden yeniden işleme bunun üstünden yapılır.
- **FR-011**: Kayıt durumu şu yaşam döngüsünü izlemelidir: Beklemede → İşleniyor → Tamamlandı / Hatalı.
- **FR-012**: İçeriği değişmemiş kayıt (aynı tedarikçi + kimlik + hash) hiçbir aşamada yeniden işlenmemelidir.
- **FR-013**: İçeriği değişmiş kayıt güncelleme, yeni kayıt oluşturma olarak işlenmelidir.
- **FR-014**: "İşlenecek mi" kararı yalnızca deterministik kodla verilmelidir; hiçbir koşulda LLM/agent kararına bırakılamaz.
- **FR-015**: Ara modelde ileriye dönük opsiyonel bir barkod alanı bulunmalıdır (bugün doldurulması zorunlu değildir).

#### Domain'e yazım

- **FR-016**: Domain servislerine yazım, her biri tek bir servise bağlı ayrı akıllı yazıcılar (agent) üzerinden yapılmalıdır.
- **FR-017**: Katalog yazıcısı ürünü, stok yazıcısı stok adedini, indirim yazıcısı ürün indirimini işler; her yazıcı yalnız kendi servisine erişir.
- **FR-018**: Ham marka metni, sistemin tanımlı markalarına eşlenmelidir; eşlenemeyen kayıt hatalı sayılır (FR-020).
- **FR-019**: Yazım, servislerin mevcut arayüzleri (MCP araçları) üzerinden olmalıdır; hiçbir servisin veritabanına doğrudan erişilmez.
- **FR-025**: İndirim yüzdeli kayıt (0 < yüzde ≤ 100) ürün indirimi olarak yazılır; indirim kodu domain'e yazılmaz, ara modelde kalır.
- **FR-026**: Daha önce indirim yazılmış ürünün yeni feed'inde indirim yoksa, üründeki indirim kaldırılır.

#### Hata yönetimi ve gözlemlenebilirlik

- **FR-020**: İşlenemeyen kayıt Hatalı durumuna geçmeli, nedeni kaydedilmeli ve akışın kalanını durdurmamalıdır.
- **FR-021**: Hatalı kayıtlar sonraki aktarımda yeniden işlenmeyi denemelidir.
- **FR-022**: Her aktarım sonunda özet üretilmelidir: yeni, güncellenen, atlanan ve hatalı kayıt sayıları (tedarikçi kırılımıyla).
- **FR-023**: Operatör ara depodaki kayıtları durumlarına göre listeleyip tekil kaydın ham verisini görebilmelidir.
- **FR-024**: Aynı anda yalnız bir aktarım çalışabilmelidir; çakışan tetikleme reddedilir.

### Key Entities

- **Tedarikçi (simüle)**: Dış ürün kaynağı kimliği; feed biçimi ve marka kümesiyle tanımlanır. Simülatörde yaşar.
- **Tedarikçi Ürünü**: Simülatörün yayınladığı kaynak kayıt; harici kimlik, ad, açıklama, ham marka, fiyat, stok adedi,
  opsiyonel indirim kodu + yüzdesi.
- **Ara Kayıt (StagingRecord)**: Çekilen kaydın ingestion tarafındaki izi; tedarikçi + harici kimlik anahtarı, ham veri,
  içerik izi (hash), normalize alanlar, opsiyonel barkod/indirim kodu/indirim yüzdesi, durum, işlenme zamanı ve hata nedeni.
- **Aktarım Özeti (IngestionRun)**: Bir tetiklemenin sonucu; başlangıç/bitiş, tedarikçi kırılımlı sayaçlar ve genel durum.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Üç farklı biçimdeki feed'den gelen tüm sağlam kayıtlar tek aktarımla katalog ve stokta görünür (N kayıt → N ürün).
- **SC-002**: Değişmemiş feed'lerin ikinci aktarımında domain'e sıfır yazma olur; özet tüm kayıtları "atlandı" gösterir.
- **SC-003**: Tek kaydı değişen bir feed'in aktarımında yalnız bir ürün güncellenir; kalan tüm kayıtlar atlanır.
- **SC-004**: Feed'deki bozuk kayıtlar aktarımı durdurmaz; sağlam kayıtların tamamı işlenir, bozuklar nedenleriyle listelenir.
- **SC-005**: Herhangi bir ara kaydın tedarikçiden geldiği ham hali, tedarikçiye tekrar gidilmeden görüntülenebilir.
- **SC-006**: Yeni bir tedarikçi eklemek yalnız bir çevirici (adapter) ve veri seti gerektirir; ara depo şeması değişmez.
- **SC-007**: İndirim yüzdeli kayıtların ürünlerinde aynı oranda indirim görünür; indirimi feed'den kalkan üründe kalkar.

## Assumptions

- Üç feed de tam anlık görüntüdür; delta/cursor takibi yoktur (kullanıcı kararı, 2026-07-22).
- Satıştan kalkma (delist) tespiti kapsam dışıdır; feed'den düşen ürün domain'de silinmez/pasife alınmaz.
- Category kavramı kapsam dışıdır; ayrı bir feature olarak ele alınacaktır.
- Kesişen kataloglar, barkod eşleştirme ve offer/buybox modeli kapsam dışıdır; ara modeldeki barkod alanı yalnız kapıyı açık tutar.
- n8n kullanılmaz; akış orkestrasyonu kod içindedir (bu feature 2026-07-14 n8n kararını geçersiz kılar).
- Aktarım tetiklemesi manueldir (istek üzerine); zamanlanmış çalıştırma bu kapsamda yoktur.
- Veri setlerinin içeriğini (ürün listeleri) kullanıcı hazırlar; her set makul boyuttadır (feed başına ≤ ~100 kayıt).
- Domain'e yazımda mevcut kimlik altyapısı (scope-tabanlı yetki, servis kimliği) yeniden kullanılır; yeni yetki modeli icat edilmez.
- Aktarım tetikleme/görüntüleme uçları şimdilik anonimdir (kullanıcı kararı, 2026-07-22); domain yazımları scope korumalı kalır.
- Her iki yeni proje sisteme Aspire üzerinden dahil olur ve "her context kendi veritabanı" ilkesine uyar.
- Catalog SeedData tamamen kaldırılır; ürünler yalnız tedarikçi aktarımından gelir (kullanıcı kararı, 2026-07-22).
- Feed verileri temiz ve tekdüzedir (nokta ondalık, temiz marka adı); bozuk kayıt yalnız eksik alanla simüle edilir.
- İndirim kodu bilgilendirme amaçlıdır (kampanya etiketi); kupon/kullanıcıya özel indirim bu feature'ın kapsamı dışıdır.