# Feature Specification: Id-Based Search Filters + Paging

**Feature Branch**: `068-id-based-search-paging`

**Created**: 2026-09-07

**Status**: Draft

**Input**: User description: "Arama tool'u kimlik-bazlı yapısal filtrelere geçer + DB-side filtreleme +
sayfalama (067 refinement). Ad-eşleştirme kalkar; LLM adları envanter listelerinden kimliğe çözer;
arama yalnız istenen sayfayı üretir. Kod tartışması sonucu kilitli kararlar."

**Kademe**: Tam — mevcut arama sözleşmesi kırılarak değişir (ad → kimlik parametreleri), yeni
sayfalama sözleşmesi eklenir, asistan davranış sözleşmesi değişir. (067'nin refinement'ı; tasarım
oturumda kilitlendi, belirsizlik yok.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Yazım varyantından etkilenmeyen kesin filtreleme (Priority: P1)

Müşteri sohbette bir yazar/yayınevi/kategori kısıtı verir ("H.G. Wells kitapları", "Harlequin hariç").
Asistan bu adları mağaza envanterinden KANONİK kimliğe çözer ve aramayı kimlikle yapar. Kullanıcının
noktalama/boşluk/büyük-küçük yazımı sonucu asla etkilemez; ad eşleşememesi diye bir hata sınıfı kalmaz.

**Why this priority**: 067 canlı testinde yaşanan gerçek kayıp ("H.G. Wells" ↔ "H. G. Wells" → 0
sonuç) kökten çözülür; keşif güvenilirliği kuzey-yıldızının önkoşulu.

**Independent Test**: Chat'e aynı yazar farklı yazımlarla sorulur ("H.G. Wells", "h. g. wells",
"HG Wells") — üçü de aynı kitap kümesini döndürür. Var olmayan yazar adı → dürüst "bu adla yazar
bulamadım" yanıtı (uydurma kimlik yok).

**Acceptance Scenarios**:

1. **Given** envanterde kanonik adıyla kayıtlı bir yazar, **When** müşteri adı farklı bir yazımla
   verir, **Then** asistan adı envanter listesinden kimliğe çözer ve arama o yazarın tüm kitaplarını
   döndürür.
2. **Given** envanterde karşılığı olmayan bir ad, **When** müşteri o adla kısıt verir, **Then**
   asistan kimlik çözemediğini açıkça söyler; arama uydurma/yakın kimlikle YAPILMAZ.
3. **Given** dışlama kısıtı ("X hariç"), **When** X kimliğe çözülür, **Then** dönen hiçbir üründe o
   kimlik yer almaz.

---

### User Story 2 - Uzun sonuçlarda "devamını göster" (Priority: P1)

Bir kısıt 50+ ürün döndürebilir. Asistan ilk sayfayı gösterir, toplam sayıyı söyler ve devamı olup
olmadığını belirtir; müşteri "devamını göster" dediğinde sonraki sayfa gelir — sıralama sayfalar
arasında tutarlıdır (aynı ürün iki sayfada birden görünmez, atlanmaz).

**Why this priority**: Bugün sonuç 20 ile kırpılıyor ve fazlası sessizce yok sayılıyor; müşteri
kümenin tamamına erişemiyor.

**Independent Test**: 20'den fazla kitabı olan bir kısıtla arama yapılır; ilk sayfa + toplam sayı +
"devamı var" bilgisi doğrulanır; ikinci sayfa istenince ilk sayfayla kesişmeyen, sıralamayı sürdüren
sonuçlar gelir; son sayfadan sonrası boş + "devamı yok".

**Acceptance Scenarios**:

1. **Given** kısıtı karşılayan N > sayfa-boyu ürün, **When** arama yapılır, **Then** ilk sayfa,
   toplam N ve devam işareti döner.
2. **Given** ilk sayfa gösterildi, **When** müşteri devamını ister, **Then** sonraki sayfa aynı
   sıralamanın kaldığı yerinden gelir (tekrar/atlama yok).
3. **Given** son sayfa geçildi, **When** bir sonraki sayfa istenir, **Then** boş liste + devam-yok
   işareti döner (hata değil).

---

### User Story 3 - Katalog büyüse de arama maliyeti sabit (Priority: P2)

Arama, istenen sayfayı üretmek için kataloğun tamamını işlemez; maliyet sayfa boyutuyla orantılıdır.
Temalı (anlamsal) arama da aynı sayfalama ve alaka-eşiği davranışıyla çalışmaya devam eder.

**Why this priority**: Mevcut yaklaşım her aramada tüm satılabilir kümeyi işliyor; katalog
büyüdüğünde keşif deneyimi bozulur. Kullanıcı kararı: "kullanıcı düzgün girsin (kimlikle), ben onun
için bütün veriyi çekemem."

**Independent Test**: Aynı kısıtla arama, sonuç sayfası dışında veri taşımadan yanıtlanır (teknik
doğrulama sorgu izinde); temalı arama + sayfa 2 kombinasyonu eşik davranışını koruyarak çalışır.

**Acceptance Scenarios**:

1. **Given** herhangi bir yapısal kısıt, **When** arama koşar, **Then** yanıt yalnız istenen
   sayfanın verisini içerir ve üretimi katalog boyutundan bağımsız ölçeklenir.
2. **Given** temalı arama + sayfa isteği, **When** koşar, **Then** alaka eşiği aynen uygulanır;
   eşik altı sonuç sayfalamayla "geri gelmez".

---

### Edge Cases

- Kimlik verilmiş ama artık satışta ürünü yok (yayından kalkmış) → boş sonuç + dürüst mesaj; hata değil.
- Sayfalar arasında katalog değişirse (yeni ürün yayına girdi) küçük kayma kabul edilebilir; garanti
  "aynı anlık görüntü" değil "tutarlı sıralama kuralı"dır.
- Asistan kimlik çözmeden ad ile aramaya kalkarsa arama yüzeyi ad parametresi SUNMAZ — sözleşme
  düzeyinde imkânsızdır (davranış kuralına güvenilmez).
- Aynı kişinin envanterde iki ayrı kimlikle bulunması (ör. "J.R.R. Tolkien" ve "John Ronald Reuel
  Tolkien") bu özelliğin çözdüğü bir şey DEĞİLDİR — asistan iki kimliği de gösterip seçtirir ya da
  ikisini ayrı aramalarla birleştirir (yazar birleştirme ayrı iş).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Arama yüzeyinin yapısal kimlik kısıtları (yazar/yayınevi/kategori, dahil + hariç)
  YALNIZ kimlikle çalışmalıdır; ad-tabanlı yapısal parametreler kaldırılır (kırıcı değişiklik kabul).
- **FR-002**: Asistan, kullanıcı adlarını envanter listelerinden (kanonik ad + kimlik döndüren mevcut
  keşif yüzeyleri) kimliğe çözmelidir; çözülemeyen ad için kullanıcıya dürüst geri bildirim verilir,
  uydurma/yakın kimlikle arama yapılmaz.
- **FR-003**: Arama sayfalanabilir olmalıdır: sayfa isteği + yanıtın toplam sayı, geçerli sayfa ve
  devam-var bilgisi taşıması; sıralama sayfalar arasında deterministik ve tutarlı olmalıdır.
- **FR-004**: Temalı (anlamsal) arama sayfalamayla birlikte çalışmalı; alaka eşiği ve "bulunamadı"
  dürüstlüğü (067 FR-005) aynen korunmalıdır.
- **FR-005**: Arama, istenen sayfayı üretmek için katalogdaki tüm kayıtları belleğe/işleme
  taşımamalıdır; işlenen veri sayfa + kısıtla orantılı kalmalıdır.
- **FR-006**: "Buna benzer" yüzeyi sayfalanmaz (doğası gereği en-yakın-N); mevcut davranışı değişmez.
- **FR-007**: Fiyat aralığı ve stok kısıtları mevcut anlamıyla korunur; kimlik kısıtlarıyla birlikte
  uygulanır.

### Key Entities

- **Arama isteği (değişir)**: Yapısal kısıtlar kimlik listeleri/kimlik olarak; sayfa bilgisi eklenir.
- **Arama yanıtı (değişir)**: Sonuç sayfası + toplam sayı + sayfa + devam-var; ürün kalemi 067'deki
  şekliyle (kapak görseli dahil) sürer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Aynı yazar/yayınevi kısıtı, adın en az 3 farklı makul yazımıyla (noktalama/boşluk/
  büyük-küçük) AYNI sonuç kümesini üretir; "yazım yüzünden boş sonuç" sınıfı ortadan kalkar.
- **SC-002**: 20'den fazla sonucu olan her kısıtta kullanıcı, sohbet üzerinden kümenin TAMAMINA
  sayfa sayfa erişebilir; hiçbir sonuç sessizce kaybolmaz (toplam sayı her sayfada görünür).
- **SC-003**: Ardışık sayfa istekleri kesişmez ve atlama yapmaz (deterministik sıralama), son sayfa
  sonrası istek hatasız "devamı yok" döner.
- **SC-004**: Arama yanıtının ürettiği/taşıdığı veri, katalog kayıt sayısından bağımsız olarak sayfa
  boyutuyla orantılıdır (teknik izlemede tam-küme yükleme gözlenmez).
- **SC-005**: Çözülemeyen ad senaryosunda asistan %100 dürüst geri bildirim verir; uydurma kimlikle
  arama yapılan tek bir vaka bile kabul edilmez.

## Assumptions

- Arama yüzeyinin tek üretim tüketicisi sohbet asistanı + MCP şemasını dinamik keşfeden dış
  agent'lardır; ad→kimlik geçişi kırıcı değişikliktir ve kabul edilir (sürümleme/geriye-uyum katmanı
  açılmaz).
- Ad→kimlik çözümü asistanın sorumluluğudur; envanter listeleri kanonik ad + kimlik döndürmeye devam
  eder (067'de kuruldu). Bir ad birden çok kimliğe çözülürse seçim/birleştirme asistan davranışıdır.
- "Devamını göster" akışında asistan önceki isteğin kısıtlarını sohbet bağlamından aynen tekrarlar;
  sunucu tarafında oturum/imleç durumu tutulmaz.
- Sayfa boyu mevcut sonuç-limiti alışkanlığını korur (varsayılan küçük, üst sınırlı); kesin değerler
  planlama aşamasının konusudur.
- Kapsam dışı: kategori taksonomisinin düzeltilmesi, ad-benzeri dışlama genişletmeleri ("Harlequin"
  → "Harlequin Mills & Boon"), anlamsal temsil metninin zenginleştirilmesi, yazar birleştirme.
---

> **İPTAL (2026-09-07 gece):** Bu spec, 069 kararıyla büyük ölçüde geçersizleşti — parametrik arama
> tool'u tamamen kaldırılıyor (tek text-to-SQL kapısı `query_storefront`, bkz `specs/069-query-storefront`).
> Id-bazlı parametre ve sayfalama fikirleri 069'un prompt/sorgu kalıplarında yaşamaya devam eder.
