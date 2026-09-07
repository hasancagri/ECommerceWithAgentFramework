# Feature Specification: Storefront Semantic Search

**Feature Branch**: `067-storefront-semantic-search`

**Created**: 2026-09-07

**Status**: Draft

**Input**: User description: "Storefront'a semantik arama (açıklama-embedding tabanlı 'buna benzer' ve bulanık/temalı sorgu desteği) + eksik yapısal keşif tool'ları (kategori/yazar/yayınevi listeleme) eklenir. Kod yok, brainstorm sonucu kilitli kararlar."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bulanık/temalı arama ile kitap bulma (Priority: P1)

Müşteri, sohbet asistanına tam başlık veya net bir yazar adı yerine bir tema/ruh hali tarif ederek kitap arar (örn. "kışın okunacak sürükleyici bilim kurgu, 300 TL altı, X yayınevi hariç"). Asistan bunu hem yapısal kısıtlara (fiyat, yayınevi hariç tutma) hem de anlamca en yakın kitaplara ayırarak yanıtlar.

**Why this priority**: Bu, mevcut ürün keşfinin (yalnız ad/yazar/yayınevi alt-dizge eşleşmesi) çözemediği, projenin "metin-üzeri tam keşif" kuzey-yıldızına en doğrudan hizmet eden senaryo.

**Independent Test**: Katalogda açıklaması dolu ürünler varken, temalı bir sorgu chat'e yazılır; dönen sonuçların yapısal kısıtları (fiyat/hariç-tutma) sağladığı ve anlamca sorguyla ilişkili olduğu doğrulanır. Diğer user story'ler olmadan da bağımsız çalışır ve değer üretir.

**Acceptance Scenarios**:

1. **Given** açıklaması embedding'e sahip, yayında ürünler, **When** müşteri hem yapısal (fiyat aralığı, hariç tutulacak yayınevi) hem bulanık (tema) içeren tek cümlelik bir istek yazar, **Then** sistem önce yapısal kısıtları uygular, kalan kümede anlamca en yakın sonuçları döndürür.
2. **Given** hiçbir ürünün açıklaması sorguyla anlamca yeterince yakın değilse, **When** bulanık arama çalıştırılır, **Then** sistem alakasız sonuçları "benzer bulundu" gibi sunmaz; boş/"bulunamadı" yanıtı döner.

---

### User Story 2 - Bir ürüne benzer kitapları görme (Priority: P2)

Müşteri sohbette belirli bir kitaptan bahsettikten sonra "buna benzer başka ne var" diye sorar. Asistan o kitabın açıklamasına anlamca en yakın, halihazırda satılabilir diğer kitapları listeler.

**Why this priority**: Keşif kuzey-yıldızının ikinci en değerli yüzeyi (detay-sayfası yerine sohbet üzerinden); P1'in altyapısını (embedding varlığı) yeniden kullanır, ayrı bir sorgu şekli gerektirir.

**Independent Test**: Açıklaması dolu bir ürün için "benzer" isteği chat'e yazılır; kendisi hariç, satılabilir/yayında diğer ürünlerden anlamca en yakın olanların döndüğü, ilgisiz sonuç yoksa boş döndüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** açıklaması embedding'e sahip bir ürün, **When** müşteri o ürüne benzer kitap ister, **Then** sistem kendisi hariç, yayından kaldırılmamış en yakın N ürünü döner.
2. **Given** o ürüne anlamca yeterince yakın hiçbir başka ürün yoksa, **When** benzer kitap istenir, **Then** sistem zorla en yakın-ama-alakasız sonuçları döndürmez; "benzer bulunamadı" der.

---

### User Story 3 - Kategori/yazar/yayınevi keşfi (Priority: P1)

Müşteri "hangi kategorileriniz var", "hangi yazarlardan kitap var" gibi genel keşif soruları sorar. Asistan bugün bu soruları yanıtlayamıyor (bilinen boşluk); bu story bunu kapatır.

**Why this priority**: Bilinen ve sık karşılaşılan bir keşif kırığı; P1 (semantik arama) ile birlikte MVP'nin temel iki bacağından biri, teknik olarak en basit/düşük riskli parça.

**Independent Test**: Chat'e "hangi kategoriler var" yazılır; şu an sistemin bu soruyu yanıtlayamadığı (araç eksikliği) durumdan, satılabilir ürünlerdeki gerçek kategori/yazar/yayınevi listesinin döndüğü duruma geçildiği doğrulanır. P1/P2'den bağımsız çalışır.

**Acceptance Scenarios**:

1. **Given** yayında/satılabilir ürünler farklı kategorilerde, **When** müşteri mevcut kategorileri sorar, **Then** sistem yalnız en az bir satılabilir üründe kullanılan kategorileri listeler (yayından kaldırılmış/silinmiş ürünlere ait kategoriler listelenmez).
2. Aynı davranış yazar listesi ve yayınevi listesi için de geçerlidir.

---

### Edge Cases

- Bir ürünün açıklaması sonradan boştan doluya güncellenirse (örn. import/description-fetch tamamlandığında), o ürün semantik aramada bir sonraki güncellemeden itibaren görünür hale gelir; güncelleme öncesi semantik olarak "görünmez" kalması kabul edilebilir.
- Kullanıcı "kategori X VEYA yazar Y" gibi farklı alanlar arası VEYA mantığı içeren bir istek yazarsa, sistem bunu tek bir sorguda çözmek ZORUNDA değildir — asistanın birden çok arama yapıp sonuçları kendisi birleştirmesi yeterli kabul edilir (kapsam dışı: çok-alanlı VEYA'yı tek sorguda çözen bir filtre motoru).
- Açıklaması olmayan (henüz zenginleştirilmemiş) bir ürün, semantik aramada aday olarak değerlendirilmez; yapısal aramada (ad/kategori/fiyat vb.) normal şekilde bulunmaya devam eder.
- Aynı isteğin içinde hem yapısal hem bulanık ifade karışıksa (örn. tek cümlede fiyat + yayınevi-hariç + tema), ayrıştırma hatası (yanlış alana atama) bu spesifikasyonun garanti ettiği bir şey değildir — kabul edilebilir bir hata sınıfı olarak not edilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, satılabilir/yayında her ürün için açıklama metninden türetilmiş bir anlamsal temsil (semantik profil) üretip saklamalıdır; bu temsil ürünün açıklaması değiştiğinde güncellenmelidir.
- **FR-002**: Sistem, kullanıcının serbest-metin/temalı arama isteklerini, mevcut yapısal filtrelerle (fiyat, yazar, yayınevi, kategori, stok) birlikte işleyebilmelidir — önce yapısal kısıtlar uygulanır, sonra kalan kümede anlamca en yakın sonuçlar sıralanır.
- **FR-003**: Sistem, kullanıcının bir/birden çok alanı ("şunu içermeyen") dışlamasına izin vermelidir (ör. belirli yayınevi/yazarları hariç tutma).
- **FR-004**: Sistem, belirli bir ürüne anlamca en yakın diğer satılabilir/yayında ürünleri döndürebilmelidir (kendisi hariç).
- **FR-005**: Sistem, bir arama/benzerlik isteğinde hiçbir sonuç yeterince alakalı değilse, alakasız sonuçları zorla döndürmek yerine "bulunamadı" durumunu iletmelidir.
- **FR-006**: Sistem, mevcut satılabilir/yayında ürünler üzerinden kullanılan kategori, yazar ve yayınevi listelerini sorgulanabilir kılmalıdır (yayından kaldırılmış/silinmiş ürünlerin verisi hariç).
- **FR-007**: Semantik temsil üretimi, ürünün silinmiş/yayından kaldırılmış olma durumuyla tutarlı kalmalıdır — yayından kaldırılan bir ürünün semantik verisi arama/benzerlik sonuçlarında görünmemelidir.
- **FR-008**: Sistem, mevcut (geçmiş) ürün kataloğu için semantik temsillerin toplu olarak bir kerede oluşturulabilmesini desteklemelidir (yeni özellik devreye alınırken geriye dönük doldurma).

### Key Entities *(include if feature involves data)*

- **Ürün vitrin kaydı (mevcut)**: Bugün var olan, satışa sunulan her ürünün ad/fiyat/yazar/yayınevi/kategori/stok gibi yapısal alanlarını taşıyan kayıt. Bu feature ona bir "anlamsal temsil" (açıklamadan türetilmiş) alanı ekler; kaydın yaşam döngüsü (oluşturma/güncelleme/yayından kaldırma) değişmez.
- **Anlamsal temsil**: Bir ürünün açıklama metninden türetilen, "anlamca yakınlık" hesaplamak için kullanılan veri. Kullanıcıya doğrudan gösterilmez; yalnız arama/sıralama/benzerlik hesaplarında kullanılır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Açıklaması dolu ürünler için, temalı/bulanık bir arama isteği yazıldığında, dönen sonuçların tamamı belirtilen yapısal kısıtları (fiyat aralığı, hariç tutulan yayınevi/yazar) karşılar.
- **SC-002**: "Buna benzer kitap" isteklerinin, açıklaması bulunan her ürün için, kendisini hariç tutarak sonuç üretebilmesi — açıklaması olan ürünlerin tamamı için benzerlik sorgusu çalışır durumdadır (sonucu boş olabilir, hata vermez).
- **SC-003**: "Hangi kategoriler/yazarlar/yayınevleri var" sorularının tamamı, mevcut satılabilir ürün kümesiyle tutarlı, güncel bir liste ile yanıtlanır (yayından kaldırılmış ürünlere ait değer sızmaz).
- **SC-004**: Mevcut kataloğun (binlerce ürün) geriye dönük semantik temsil doldurma işlemi, tek seferlik bir toplu işlemle dakikalar mertebesinde tamamlanır (saatler değil).
- **SC-005**: Alakasız/eşleşmeyen bir bulanık arama veya benzerlik isteğinde, kullanıcıya yanlışlıkla "benzer bulundu" izlenimi veren, aslında alakasız sonuçlar SUNULMAZ.

## Assumptions

- Ürünlerin açıklama metni ayrı bir veri-hazırlık çalışmasıyla (mevcut, bu spesifikasyonun dışında yürüyen bir iş) dolduruluyor/dolduruluyor olacak; bu feature açıklaması hâlâ boş olan ürünleri semantik aramada aday saymaz, bu durum hata değildir.
- "Anlamca yakınlık" hesaplaması için harici bir üçüncü taraf yapay-zekâ hizmeti (üretim/summarization değil, yalnız metin→temsil dönüşümü) kullanılacağı varsayılır; bu, projede hâlihazırda kullanılan yapay-zekâ altyapısının bir uzantısıdır, yeni bir dış sistem sınıfı eklemez.
- Farklı alanlar arası VEYA mantığı (ör. "kategori X VEYA yazar Y") gerektiren istekleri tek bir sorguda çözmek bu feature'ın kapsamı dışıdır; sohbet asistanının birden çok arama yapıp sonucu kendi başına birleştirmesi yeterli kabul edilir.
- Kategori/yazar/yayınevi listeleme, yalnız en az bir satılabilir/yayında üründe fiilen kullanılan değerleri gösterir; ürün yönetimi tarafında tanımlı ama hiç satışta olmayan değerler bu listelerde görünmez.
- Bu spesifikasyon, mevcut kataloğun geriye dönük semantik temsil doldurma işini bir defalık bir görev olarak kapsar; ancak bu doldurma işinin ön koşulu olan "tüm ürünlerin açıklama metninin hazır olması" ayrı, halihazırda süren bir veri-hazırlık çalışmasıdır ve bu spesifikasyonun tamamlanma kriteri değildir.