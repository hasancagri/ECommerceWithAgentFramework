# Feature Specification: Tedarikçi Feed'i = Stoğun Tek Otoritesi

**Feature Branch**: `014-supplier-stock-authority`

**Created**: 2026-07-26

**Status**: Draft

**Input**: User description: "Tedarikçi feed'i, ürün stok adedinin tek otoriter
kaynağı olsun. Stok yalnızca IngestionAgent workflow'undaki yeni bir StockWrite
executor üzerinden yazılsın; başka hiçbir yol stoğa yazmasın."

## Artefakt Kademesi

**Tam** (anayasa "Artefakt Ölçekleme"). Gerekçe: servisler-arası kontrat değişir
(`ProductCreatedEvent`'ten stok alanı çıkar) ve iki context'in (Catalog, Stock)
stok-akışı davranışı değişir. Küçük kademe koşulları (servisler-arası etki yok)
bozulduğu için tam akış işletilir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Yeni tedarikçi ürününün başlangıç stoğu (Priority: P1)

Tedarikçi feed'i daha önce görülmemiş bir ürün getirdiğinde, o ürünün stok adedi
sistemde otomatik oluşur ve bu değer feed'in bildirdiği adede eşittir. Stok yazımı
yalnızca ingestion akışındaki StockWrite adımından geçer.

**Why this priority**: Ürünler yalnız tedarikçiden geldiği için, stoğun feed ile
oluşması temel işlevdir; bu olmadan yeni ürün satılamaz durumda kalır.

**Independent Test**: Feed'e yeni bir kayıt eklenip çekim tetiklenir; ürünün
sistemdeki stok adedinin feed değerine eşit olduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** feed'de yeni bir ürün (StockQuantity=N), **When** çekim işlenir,
   **Then** ürünün sistemdeki stok adedi N olur.
2. **Given** ürün katalog yazımı başarısız, **When** çekim işlenir, **Then** stok
   yazılmaz; mesaj yeniden denenir/DLQ'ya düşer (yarım durum kalmaz).

---

### User Story 2 - Tedarikçi stok değişikliğinin re-sync'i (Priority: P1)

Tedarikçi mevcut bir ürünün kaydını değiştirdiğinde (stok veya başka alan), çekim o
kaydı yeniden yayınlar ve sistemdeki stok adedi feed'in son değerine eşitlenir. Feed,
stoğun tek otoritesidir.

**Why this priority**: "Feed = tek otorite" kararının çalışan yüzü budur; stok
adedinin güncel kalması satış doğruluğu için P1'dir.

**Independent Test**: İşlenmiş bir ürünün feed'deki stoğu değiştirilip çekim
tetiklenir; sistemdeki stok yeni değere eşitlenir.

**Acceptance Scenarios**:

1. **Given** stoğu M olan bir ürün, **When** feed'de aynı ürün StockQuantity=K ile
   değişir ve çekim işlenir, **Then** sistemdeki stok K olur.
2. **Given** feed'de değişmemiş bir ürün, **When** çekim işlenir, **Then** o ürün
   için stok yazımı tetiklenmez (gereksiz yazım yok).

---

### User Story 3 - Stoğa tek yazım yolu garantisi (Priority: P2)

Stok adedi feed dışında hiçbir yoldan yazılamaz: katalog "ürün oluştu" olayına bağlı
stok tohumlama yolu ve manuel mutlak-stok atama REST ucu kaldırılır. Katalog artık
stok adedi taşımaz.

**Why this priority**: "Başka hiçbir yerden dolmasın" kısıtının kalıcı garantisi;
gelecekte yeni bir stok-yazım yolu sızmasını engeller. İşlevi P1'ler sağlar, bu
hikâye onu izole eder.

**Independent Test**: Kod/akış incelemesiyle feed dışında stok yazan yol olmadığı;
katalog olayında ve manuel uçta stok yazımı bulunmadığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir ürün katalogta oluşturulur, **When** olay yayılır, **Then** bu olay
   stok kaydını tohumlamaz (stok yalnız StockWrite'tan yazılır).
2. **Given** eski manuel mutlak-stok atama ucu, **When** çağrılır, **Then** uç artık
   yoktur (stok dışarıdan elle set edilemez).

---

### Edge Cases

- Feed, OnHand'i aktif rezervasyonların altına düşürürse: satılabilir adet 0'a
  kırpılır ve durum "oversold" olarak tespit edilir; müşteri mevcut stoktan fazlasını
  sipariş edemez (checkout korunur).
- Aynı ürün olayı iki kez teslim edilirse: stok mutlak değere set edildiğinden ikinci
  teslim aynı değeri yazar; nihai durum tek teslimle aynıdır.
- Sipariş Commit'i OnHand'i yerel olarak düşürdükten sonra tedarikçi aynı ürünü
  değiştirirse: OnHand feed değerine re-sync olur; aradaki yerel düşüş OnHand'de
  geçersiz kalır (feed otorite — bilinçli karar).
- Katalogta ürün var ama stok kaydı yoksa (eski/eksik veri): StockWrite stok kaydını
  açıp feed adedine ayarlar (upsert).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, tedarikçi feed'inden gelen her yeni/değişen ürün için stok
  adedini ingestion akışındaki StockWrite adımıyla yazmalıdır.
- **FR-002**: StockWrite, katalog yazımından dönen ürün kimliğini ve olaydaki stok
  adedini kullanarak stoğu **mutlak değere** (overwrite) ayarlamalıdır.
- **FR-003**: Stok adedi feed dışında hiçbir yoldan yazılmamalıdır: katalog "ürün
  oluştu" olayına bağlı stok tohumlama yolu ve manuel mutlak-stok atama REST ucu
  kaldırılmalıdır.
- **FR-004**: Katalog context'i stok adedi taşımamalıdır: katalog yazım çağrısından
  başlangıç-stok argümanı ve "ürün oluştu" olayından stok alanı çıkarılmalıdır.
- **FR-005**: Tedarikçi bir ürünü değiştirdiğinde OnHand feed değerine re-sync
  olmalıdır; aradaki yerel Commit düşüşleri OnHand'de geçersiz kalır (feed otorite).
- **FR-006**: Feed, OnHand'i aktif rezervasyonların altına düşürse bile checkout
  güvenliği korunmalıdır: satılabilir adet 0'a kırpılır, oversold durumu tespit edilir.
- **FR-007**: StockWrite yalnız katalog yazımı başarılıysa (ürün kimliği varsa)
  çalışmalıdır; başarısız stok yazımı mevcut ingestion hata modeliyle (retry/DLQ)
  ele alınmalıdır.
- **FR-008**: Stok değişimi, mevcut olduğu gibi Storefront okuma-modelini beslemeye
  devam etmelidir (stok değişti olayı yayılır).
- **FR-009**: Çift-teslim edilen ürün olayı stoğu bozmamalıdır (mutlak set aynı değeri
  yazar; idempotent).
- **FR-010**: Değişmemiş feed kayıtları stok yazımını tetiklememelidir (mevcut
  snapshot-diff kapısı korunur).

### Key Entities *(include if feature involves data)*

- **ProductStock (Stock context)**: Ürünün fiziksel stoğu (OnHand) + aktif
  rezervasyonlar. Satılabilir = OnHand − aktif rezervasyon. Mevcut yetenekler yeterli:
  mutlak stok atama (negatif-yasak kuralıyla), satılabilir hesabı, oversold tespiti.
- **Tedarikçi ürün anlık görüntü olayı**: feed'den kanonikleşen kayıt; ürün kimliği
  (harici) + stok adedi taşır. Stok yazımının kaynağıdır.
- **Katalog ürün-oluştu olayı**: bu feature'la stok alanını kaybeder; artık yalnız
  katalog kimliği/tanımlayıcı bilgi taşır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tedarikçiden gelen her yeni ürün, feed işlendikten sonra feed'in
  bildirdiği adede eşit stokla görünür (uyum %100).
- **SC-002**: Feed dışında stoğa yazan hiçbir yol kalmaz (kod/akış incelemesinde
  alternatif stok-yazım yolu sayısı = 0).
- **SC-003**: Tedarikçi bir ürünün stoğunu güncellediğinde, feed işlendikten sonra
  sistemdeki stok yeni feed değerine eşitlenir.
- **SC-004**: Aktif sepet rezervasyonu olan bir üründe feed düşük stok bildirse bile
  müşteri mevcut stoktan fazlasını sipariş edemez (oversell = 0).
- **SC-005**: Aynı ürün feed'i art arda iki kez işlendiğinde stok adedi tek işlemeyle
  aynı kalır (mükerrer stok değişimi yok).

## Assumptions

- Ürünler yalnızca tedarikçi feed'inden gelir; manuel ürün oluşturma bu feature'ın
  kapsamı dışındadır (kullanıcı kararı).
- Bu feature, 012 (stok rezervasyonu) "Model C — feed stoğu ezmez" duruşunu bilinçli
  olarak tersine çevirir; 012 spec'indeki ilgili not bu davranışa göre güncellenmelidir.
- Mevcut ProductStock aggregate davranışları (mutlak set, oversell tespiti, satılabilir
  hesabı) yeterlidir; yeni aggregate/invariant gerekmez.
- Stok yazım yolunun yetki modeli değişmez (mevcut durumu korur); bu feature yalnız
  "kim/nereden yazar" topolojisini değiştirir, yetki kapsamını değil.
- Storefront okuma-modeli stok-değişti olayına abonedir; yeni yazım yolu bu olayı
  yaymaya devam ettiği için Storefront'ta regresyon beklenmez.