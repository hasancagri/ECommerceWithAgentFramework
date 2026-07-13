# Feature Specification: Product Enrichment Agent (Ürün Zenginleştirme Agent'ı)

**Feature Branch**: `002-product-enrichment-agent`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "Catalog ürün enrichment agent'ı. Eksik ürünleri (boş
Description ve/veya ImageUrl) bir AI agent otomatik tamamlar: hem gerçekçi açıklama
metni ÜRETİR hem de gerçek bir ürün GÖRSELİ üretir (placeholder değil) ve Catalog'a
yazar; böylece Product tamamlanır ve satışa çıkar. Feature 001'in tamlık kapısı
tetikleyicidir: 30 seed ürün şu an eksik/satış-dışı."

## Tier (Artefakt Ölçekleme)

**Tam** — yeni bir agent bileşeni, üretilen görselin saklanmasında olası
servisler-arası etki, AI üretim yeteneği ve çözülmemiş kararlar var. Bu yüzden tam
akış (spec → plan → tasks) işletilir. (Karş. 001 "Küçük"tü.)

## Scope Note

Bu feature, **eksik ürünleri otomatik tamamlayan zenginleştirme agent'ını** kapsar —
Feature 001'in "Out of Scope" olarak ayırdığı parça. 001 satılabilirlik *kuralını*
tanımlar (eksik ürün satışa çıkamaz); bu feature o eksikleri **AI ile doldurarak**
ürünleri satışa-hazır hale getirir. Agent, Catalog bounded context'ine yalnızca
kendi sözleşmesi (MCP/API) üzerinden yazar; Catalog veritabanına doğrudan dokunmaz.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eksik bir ürün AI ile tamamlanıp satışa çıkar (Priority: P1)

Bir operatör (veya otomatik tetik) eksik bir ürünü zenginleştirme için işaret eder.
Agent, ürünün bilinen bilgilerinden (ad, marka, fiyat) yola çıkarak **alakalı bir
açıklama metni** ve **gerçek bir ürün görseli** üretir, bunları ürüne yazar. Ürün
böylece tam hale gelir ve (aktifse) müşteri aramalarında satışta görünmeye başlar.

**Why this priority**: Feature'ın çekirdek değeri budur ve tek başına iş üretir. Bu
slice olmadan eksik ürünler elle doldurulmadıkça sonsuza dek satış-dışı kalır. Tek
bir ürünün uçtan uca tamamlanması, tüm mekanizmayı doğrular.

**Independent Test**: Eksik (açıklama boş, görsel yok) tek bir aktif ürün alınır;
agent o ürün için tetiklenir; sonrasında ürünün açıklama + görselinin dolduğu ve
müşteri aramasında satışta göründüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** açıklaması ve görseli eksik, aktif bir ürün, **When** agent o ürün için zenginleştirme yapar, **Then** ürün alakalı bir açıklama ve gerçek bir görsel kazanır ve müşteri aramasında satışta görünür.
2. **Given** eksik bir ürün, **When** agent yalnızca açıklamayı üretebilir ama görsel üretimi başarısız olur, **Then** ürün eksik kalır ve satışta görünmez (kısmi/sahte veriyle satışa çıkmaz).
3. **Given** üretilen görsel, **When** ürün müşteriye listelenir, **Then** görsel gerçek/görüntülenebilir bir üründür — genel bir placeholder değildir.

---

### User Story 2 - Eksik envanterin toplu zenginleştirilmesi (Priority: P2)

Bir operatör, kataloğu satışa hazırlamak için tüm eksik ürünleri (ör. seed edilen 30
ürün) toplu olarak zenginleştirir. Agent eksikleri tarar, sırayla işler ve her ürün
için başarı/başarısızlık raporlar; katalogdaki satışa-hazır ürün sayısı belirgin
şekilde artar.

**Why this priority**: US1 tek ürünü kanıtlar; bu slice gerçek iş problemini çözer —
200 eksik ürünü elle doldurmak pratik değildir. US1'in üstüne kurulur ve bağımsız
test edilebilir.

**Independent Test**: Katalog birden çok eksik ürünle doldurulur; toplu zenginleştirme
çalıştırılır; sonrasında eksik ürünlerin büyük çoğunluğunun tam ve satışta olduğu,
sonucun ürün başına başarı/başarısızlık içerdiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** çok sayıda eksik ürün, **When** toplu zenginleştirme çalıştırılır, **Then** eksik ürünlerin büyük çoğunluğu tam ve (aktifse) satışta hale gelir.
2. **Given** toplu çalıştırma, **When** bazı ürünlerin üretimi başarısız olur, **Then** çalıştırma durmaz; başarısızlar eksik kalır, sonuç ürün başına durumu bildirir.

---

### User Story 3 - Güvenli ve tekrar-edilebilir zenginleştirme (Priority: P3)

Zenginleştirme, mevcut veriyi bozmadan ve tekrar çalıştırıldığında ek zarar/masraf
üretmeden çalışır: zaten dolu alanların üzerine yazılmaz, zaten tam ürünler atlanır,
başarısızlıklar ürünü önceki durumunda bırakır.

**Why this priority**: Operasyonel güven için değerli ama çekirdek değer US1/US2'dir.
Toplu ve tekrarlı çalıştırmada veri güvenliği kritik olur, bu yüzden ayrı ele alınır.

**Independent Test**: Bir kısmı tam bir kısmı eksik ürünlerle zenginleştirme iki kez
çalıştırılır; tam ürünlerin dokunulmadığı, ikinci çalıştırmanın yeni değişiklik
üretmediği (idempotent) doğrulanır.

**Acceptance Scenarios**:

1. **Given** açıklaması zaten dolu ama görseli eksik bir ürün, **When** agent zenginleştirir, **Then** mevcut açıklama korunur, yalnızca eksik görsel doldurulur.
2. **Given** zaten tam bir ürün, **When** zenginleştirme çalışır, **Then** ürün atlanır ve içeriği değişmez.
3. **Given** bir önceki zenginleştirme, **When** aynı küme yeniden çalıştırılır, **Then** yalnızca hâlâ eksik olanlar işlenir; tam olanlar tekrar üretilmez.

---

### Edge Cases

- **Kısmi başarı**: Açıklama üretildi ama görsel başarısız (veya tersi) → ürün eksik kalır; tamlık iki alanı da gerektirir (001, FR). Başarılı alan yazılabilir ama ürün satışa çıkmaz.
- **Zaten dolu alan**: Ürünün açıklaması veya görseli zaten varsa üzerine yazılmaz (insan/mevcut içerik korunur).
- **Yetersiz bağlam**: Ürünün adı/markası alakalı içerik üretmeye yetmiyorsa, agent düşük-kaliteli/generic içerik yayınlamaktansa o alanı eksik bırakır.
- **Eş-zamanlı/tekrar tetik**: Aynı ürün için çakışan zenginleştirme çift üretim veya çelişki yaratmamalı.
- **Üretim↔upload dikişi**: Görsel üretildi (paralı, bellekte) ama File'a upload patlarsa retry edilir; tükenirse ürün eksik kalır, sonraki koşu yeniden üretir.
- **Öksüz görsel**: File'a yazıldı ama Catalog ImageUrl yazımı patlarsa görsel öksüz kalır; ProductId-idempotency sonraki koşuda onu yeniden kullanır.
- **Ürün arada silinir/pasifleşir**: Zenginleştirme sırasında ürün silinirse/deaktif olursa, yazma sessizce başarısız olmalı veya durum tutarlı kalmalı.
- **Maliyet/hız sınırı**: Toplu çalıştırma (30 ürün) makul sürede ve üretim sağlayıcısının sınırları içinde kalmalı.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, satışa-hazır olmayan (açıklaması ve/veya görseli eksik) ürünleri zenginleştirme adayı olarak belirleyebilmelidir.
- **FR-002**: Agent, hedeflenen eksik bir ürün için ürünün bilinen bilgilerinden (ad, marka vb.) türeyen, **alakalı ve boş olmayan** bir açıklama üretmelidir. Açıklama **en fazla 100 karakter** olmalıdır (bu sınırı üreten AI garanti eder).
- **FR-003**: Agent, hedeflenen eksik bir ürün için katalogda gösterilebilir **gerçek bir ürün görseli** üretmelidir — genel bir placeholder değil.
- **FR-004**: Agent, ürettiği açıklama ve görseli ürüne Catalog'un **kendi tamamlama sözleşmesi üzerinden** yazmalı; böylece ürünün satışa-hazırlığı (001'deki invariant) yeniden hesaplanır. Agent Catalog veritabanına doğrudan erişemez.
- **FR-005**: Agent yalnızca **eksik** alanları doldurmalı; ürünün zaten dolu açıklama veya görselinin üzerine yazmamalıdır.
- **FR-006**: Bir alanın üretimi başarısız olursa, o alan önceki (eksik) durumunda kalmalı; agent, ürünü yanlışlıkla satışa çıkaracak kısmi/sahte veri yayınlamamalıdır.
- **FR-007**: Agent, ürünleri **toplu** işleyebilmeli ve her ürün için başarı/başarısızlık sonucunu raporlamalıdır; tek bir başarısızlık toplu çalıştırmayı durdurmamalıdır.
- **FR-008**: Zenginleştirme yazma işlemi, projenin **scope-tabanlı yetki** modeline uygun bir Catalog yazma yetkisiyle gerçekleşmelidir.
- **FR-009**: Zenginleştirme **idempotent** olmalıdır: zaten tam olan ürünler atlanır; tekrar çalıştırma ek değişiklik/masraf üretmez.
- **FR-010**: Görsel üretimi ve File'a yazımı **ProductId'ye göre idempotent** olmalı; ürün için asset zaten varsa yeniden üretilmez (çift üretim/maliyet önlenir).
- **FR-011**: Geçici üretim/yazma hataları retry edilmeli; kalıcı hata ilgili alanı eksik bırakır ve ürün başına raporlanır (FR-007), toplu koşu durmaz.

### Key Entities *(include if feature involves data)*

- **Product (Ürün)**: Catalog'daki satılabilir kalem. Bu feature için ilgili nitelikler: ad, marka, fiyat (üretim için bağlam); **açıklama** ve **görsel** (agent'ın doldurduğu eksik alanlar). Satışa-hazırlık bu alanlardan türetilir (001).
- **Enrichment Job/Run (Zenginleştirme Çalıştırması)**: Bir veya çok ürünü kapsayan zenginleştirme işi; ürün başına sonuç (başarılı/başarısız/atlandı) taşır. Kalıcılığı ve ayrıntısı plan'da netleşir.

## Deferred Decisions (HOW — `/speckit-plan`'da çözülecek)

Bunlar WHAT/WHY değil, uygulama (HOW) kararlarıdır; bilinçli olarak plan aşamasına
bırakılmıştır. Spec bunlar olmadan da eksiksizdir.

- **D1 — Tetikleme mekanizması**: Manuel (operatör komutu/MCP), event-driven (ürün eksik oluşturulunca RabbitMQ event'i), veya batch/scheduled. US1 (tek) vs US2 (toplu) vurgusunu etkiler.
- **D2 — Agent yerleşimi**: Yeni bir `src/agents` projesi mi, yoksa mevcut ChatAgent içinde ayrı bir agent mi. (Anayasa: agent tipleri Singleton; bkz. agent-constants yerleşimi.)
- **D3 — Üretilen görselin saklanması**: File servisi entegrasyonu mu, yoksa başka bir saklama/adresleme mi. Bounded context sınırları (anayasa I) burada belirleyici.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Toplu zenginleştirme sonrası hedeflenen eksik ürünlerin en az %95'i tam (açıklama + görsel dolu) hale gelir ve (aktifse) müşteri aramasında satışta görünür.
- **SC-002**: Seed edilen 30 ürün için satışa-hazır sayısı, tek bir toplu çalıştırmayla 0'dan en az 28'e çıkar.
- **SC-003**: Tamamlanan hiçbir ürün genel placeholder görselle veya boş/generic açıklamayla işaretlenmez (placeholder oranı %0).
- **SC-004**: Zaten tam olan hiçbir ürünün mevcut açıklama/görseli agent tarafından değiştirilmez (üzerine-yazma oranı %0).
- **SC-005**: Bir alanın üretimi başarısız olduğunda ürün yanlışlıkla satışa çıkmaz (kısmi/hatalı veriyle satılabilir işaretlenme oranı %0).
- **SC-006**: Zenginleştirme yeniden çalıştırıldığında zaten tam ürünler atlanır; ikinci çalıştırma yeni içerik üretmez (idempotent).

## Assumptions

- **001 tamlık kapısı yerinde ve değişmez**: Bu feature kuralı değiştirmez; yalnızca eksikleri doldurarak ürünleri kuralın "satışta" tarafına taşır.
- **Mevcut alanlar yeterli bağlam sağlar**: Ürünün adı/markası, alakalı açıklama ve görsel üretmek için yeterli girdi kabul edilir.
- **Bir AI üretim yeteneği mevcuttur**: Metin ve görsel üretimi için bir model/servis erişilebilir (Microsoft Agent Framework hattı); sağlayıcı seçimi plan'da netleşir.
- **Yazım Catalog sözleşmesi üzerinden**: Agent, Catalog'a MCP/API ile yazar; doğrudan DB erişimi yoktur (anayasa I).
- **İçerik doğrudan yayınlanır**: v1'de insan onay kuyruğu yoktur; tamamlanan ürün (001 gereği) otomatik satışa çıkar.
- **BC'ler arası atomiklik yoktur**: Catalog ve File yazımları tek transaction'da değil; tutarlılık invariant (kısmi durum satışa çıkamaz) + idempotent-retry ile sağlanır.
- **Agent kalıcı durum tutmaz**: Üretilen byte yalnız bellekte; tek fiziki depo File.Api'dir. Öksüz asset'ler v1'de reconcile edilmez (ProductId-idempotency yeniden kullanır).

## Out of Scope (ayrı feature/gelecek)

- **ChatAgent'ın kullanıcıya dönük sohbeti / alışveriş asistanı** akışı.
- **İçerik moderasyonu / insan-in-the-loop onay** kuyruğu.
- Açıklama ve görsel **dışındaki** alanların (fiyat, SKU, marka) üretimi/düzeltilmesi.
- **Çok dilli** açıklama üretimi (v1 tek dil).
- Üretilen içeriğin **kalite puanlama/A-B testi** ile optimize edilmesi.
