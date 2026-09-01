# Feature Specification: Kişisel Ana Sayfa (Sipariş-Temelli Heuristik Feed)

**Feature Branch**: `054-personal-home-feed`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "Kişiselleştirilmiş ana sayfa (heuristik, motor yok): WebApp ana
sayfasından 'öne çıkan kitaplar' / 'tüm kitaplar' vitrini kaldırılır; ana sayfa yalnız kullanıcıya
özel kitap listesi gösterir. Kişiselleştirme kaynağı: kullanıcının geçmiş siparişleri. Storefront,
Order'ın mevcut OrderCompleted fanout event'ini tüketerek kullanıcı-satın-alma profili biriktirir.
Yeni kişisel feed query endpoint'i: profildeki ürünlerin kategori + yazarlarını çıkarır, o
kategori/yazarlardan kullanıcının HENÜZ ALMADIĞI kitapları döner. Sinyalsiz kullanıcı: boş durum +
kategori yönlendirme kartları, fallback ürün listesi YOK. 'Tüm Kitaplara Göz At' bağlantısı dahil
genel vitrin öğeleri ana sayfadan kalkar. Mevcut liste endpoint'i ve kategori/yazar/yayınevi
filtreli gezinme AYNEN KALIR. Python/ML/RecoTrainer kapsam dışı; tamamen .NET içi heuristik."

**Kademe**: Tam — yeni kalıcı veri (kullanıcı satın-alma profili), servisler-arası event tüketimi
(mevcut sipariş-tamamlandı olayına yeni tüketici) ve yeni endpoint kontratı (kişisel feed) var.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Alışveriş geçmişi olan kullanıcı kişisel vitrin görür (Priority: P1)

Daha önce sipariş vermiş bir kullanıcı ana sayfayı açtığında, genel bir kitap vitrini yerine
yalnız kendisine özel bir liste görür: geçmişte satın aldığı kitapların kategorilerinden ve
yazarlarından, henüz satın almadığı kitaplar.

**Why this priority**: Feature'ın varlık nedeni; ana sayfanın kişiselleşmesi tek başına MVP'dir.

**Independent Test**: Siparişi tamamlanmış bir kullanıcıyla ana sayfa açılır; listedeki her
kitabın kategori ya da yazarının kullanıcının geçmişiyle eşleştiği, hiçbirinin satın alınmış
olmadığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** kullanıcının tamamlanmış siparişinde X kategorisinden bir kitap var, **When** ana
   sayfayı açar, **Then** listede X kategorisinden henüz almadığı kitaplar görünür.
2. **Given** kullanıcının geçmişinde Y yazarından bir kitap var, **When** ana sayfayı açar,
   **Then** Y yazarının almadığı diğer kitapları listede görünür.
3. **Given** kullanıcı bir kitabı (ya da varyantını) satın almış, **When** ana sayfayı açar,
   **Then** o kitap ve varyant ailesi listede görünmez.
4. **Given** kullanıcı yeni bir sipariş tamamlar, **When** kısa bir süre sonra ana sayfayı
   yeniler, **Then** yeni siparişin kategorileri/yazarları listeye yansımıştır.

---

### User Story 2 - Sinyalsiz kullanıcı boş durum + kategori yönlendirmesi görür (Priority: P2)

Anonim ziyaretçi ya da henüz hiç sipariş vermemiş üye ana sayfayı açtığında ürün listesi görmez;
"keşfe başla" niteliğinde bir boş durum ve kategori yönlendirme kartları görür.

**Why this priority**: Kişisel listenin tamamlayıcısı; kişiselleşme olmadan da ana sayfanın
çökmemesi/boş kalmaması için gereklidir ama tek başına değer üretmez.

**Independent Test**: Login'siz (ya da siparişsiz yeni üye ile) ana sayfa açılır; hiçbir ürün
kartı görünmediği, kategori yönlendirme kartlarının göründüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** anonim ziyaretçi, **When** ana sayfayı açar, **Then** ürün listesi yoktur; boş durum
   mesajı + kategori yönlendirme kartları görünür.
2. **Given** siparişsiz yeni üye, **When** ana sayfayı açar, **Then** aynı boş durum görünür
   (fallback ürün listesi YOK).
3. **Given** boş durumdaki ziyaretçi, **When** bir kategori kartına tıklar, **Then** o kategorinin
   mevcut liste sayfasına gider.

---

### User Story 3 - Genel vitrin öğeleri kalkar, gezinme bozulmaz (Priority: P3)

Ana sayfadaki "öne çıkan kitaplar" vitrini, "Tüm Kitaplara Göz At" bağlantısı ve navbar'daki
"Tüm Kitaplar" bağlantısı kaldırılır. Kategori/yazar/yayınevi üzerinden gezinme ve mevcut ürün
listeleme sayfaları aynen çalışmaya devam eder.

**Why this priority**: Görünür temizlik; P1/P2 ile birlikte anlam kazanır ama bağımsız
doğrulanabilir.

**Independent Test**: Ana sayfa kaynağında genel vitrin bölümü ve "Tüm Kitaplara Göz At"
bağlantısı bulunmadığı; kategori sayfalarının değişiklik öncesiyle aynı davrandığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** herhangi bir kullanıcı, **When** ana sayfayı açar, **Then** "öne çıkan kitaplar"
   bölümü ve "Tüm Kitaplara Göz At" bağlantısı yoktur; navbar'da "Tüm Kitaplar" bağlantısı yoktur.
2. **Given** herhangi bir kullanıcı, **When** kategori/yazar/yayınevi filtreli listeye gider,
   **Then** liste değişiklik öncesiyle aynı çalışır.

---

### Edge Cases

- Kullanıcının geçmişindeki kategorilerden/yazarlardan almadığı kitap kalmadıysa (hepsi alınmış ya
  da satıştan düşmüş): boş durum + kategori kartları gösterilir (P2 ile aynı görünüm).
- Satın alınan ürün artık vitrinde yoksa (satış dışı): sinyal üretmeye devam edebilir ama listede
  ölü kart üretmez; yalnız vitrinde satılabilir kitaplar önerilir.
- Aynı kitap hem kategori hem yazar eşleşmesiyle bulunursa listede TEK kez görünür.
- Sipariş tamamlandı sinyali gecikir/tekrarlanırsa: profil aynı ürünü iki kez saymaz (idempotent);
  gecikme yalnız listenin geç güncellenmesine yol açar, hataya değil.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ana sayfa yalnız kullanıcıya özel kitap listesi gösterir; "öne çıkan kitaplar"
  vitrini, filtresiz "tüm kitaplar" kullanımı, "Tüm Kitaplara Göz At" bağlantısı ve navbar'daki
  "Tüm Kitaplar" bağlantısı kaldırılır.
- **FR-002**: Sistem, tamamlanan siparişlerden kullanıcı başına satın alınan ürünlerin kalıcı bir
  profilini biriktirir; aynı ürünün tekrar bildirimi profili bozmaz (idempotent).
- **FR-003**: Kişisel liste, profildeki ürünlerin kategorilerinden VE yazarlarından, kullanıcının
  henüz satın almadığı kitaplardan oluşur.
- **FR-004**: Satın alınmış bir kitap ve onun varyant ailesindeki diğer üyeler kişisel listede
  önerilmez.
- **FR-005**: Kişisel liste mevcut varyant/aile gruplama kurallarıyla sunulur (aile = tek kart);
  aynı kitap listede tek kez görünür.
- **FR-006**: Sinyalsiz kullanıcıya (anonim ya da profili boş) ürün listesi gösterilmez; yalnız
  boş durum mesajı gösterilir. Fallback ürün listesi ve kategori kartları YOKTUR; genel gezinme
  navbar'daki kategori/yazar/yayınevi girişlerinden yapılır. (Rev: kullanıcı kararı 2026-09-01 —
  ana sayfada kategori kartları da istenmedi.)
- **FR-007**: Mevcut ürün listeleme ve kategori/yazar/yayınevi filtreli gezinme davranışı
  değişmez; bu feature yalnız ana sayfanın içeriğini değiştirir.
- **FR-008**: Kişisel liste yalnız kimliği doğrulanmış kullanıcı adına üretilir; anonim istek
  kişisel liste alamaz (boş durum görür).
- **FR-009**: Kişisel liste deterministik sıralanır: yazar eşleşmesi kategori eşleşmesinden önce
  gelir; eşitlikte yorum puanı yüksek olan, sonra ada göre alfabetik önce gelir. (Vitrin "eklenme
  tarihi" bilgisi taşımadığından yenilik tiebreak'i kapsam dışı.)
- **FR-010**: Kişisel liste sabit boyutlu tek sayfadır (12 kart); sayfalama yoktur. Liste
  boyutundan az eşleşme varsa olanlar gösterilir, genel ürünlerle TAMAMLANMAZ.

### Key Entities

- **Kullanıcı Satın-Alma Profili**: Kullanıcı başına satın alınmış ürünlerin kümesi; tamamlanan
  siparişlerden beslenir, kalıcıdır. Kişisel listenin tek sinyal kaynağıdır.
- **Kişisel Feed**: Profilden türetilen, kullanıcıya özel kitap listesi; kalıcı değil, istek
  anında hesaplanır.
- **Kategori Yönlendirme Kartları**: Boş durumda gösterilen, mevcut kategori gezinmesine bağlanan
  kartlar; mevcut kategori verisinden beslenir, yeni veri üretmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Siparişli kullanıcının ana sayfasındaki her kitap, kullanıcının geçmişindeki en az
  bir kategori ya da yazarla eşleşir ve hiçbiri satın alınmış değildir (%100).
- **SC-002**: Sipariş tamamlandıktan sonra 1 dakika içinde yeni sinyaller kişisel listeye yansır.
- **SC-003**: Anonim ya da siparişsiz kullanıcı ana sayfada hiçbir ürün kartı görmez; boş durum +
  kategori yönlendirmesi görür (%100).
- **SC-004**: Kategori/yazar/yayınevi liste sayfaları değişiklik öncesiyle aynı sonuçları döner
  (regresyon yok).

## Assumptions

- Profil bu feature'ın açılışından itibaren tamamlanan siparişlerden beslenir; eski siparişler
  için geriye dönük doldurma (backfill) v1 kapsamı DIŞIdır.
- Kişisel liste boyutu 12 karttır (ana sayfa vitrin boyutu); sayfalama ihtiyacı doğarsa ayrı
  feature'dır.
- "Satın alındı" sinyali yalnız başarıyla tamamlanan (onaylanmış) siparişlerden üretilir; iptal
  edilen siparişler profil üretmez.
- Sinyal kaynağı yalnız sipariş geçmişidir; gezinme/tıklama sinyali (dış analitik) bu feature'da
  kullanılmaz. Python/ML/öneri motoru kapsam dışıdır.
- Boş durumdaki kategori kartları mevcut kategori/facet verisinden gelir; yeni içerik yönetimi
  (elle kart seçimi) kapsam dışıdır.