# Feature Specification: Son Gezdiklerim (Cihaz-Yerel Şerit)

**Feature Branch**: `055-recently-viewed`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "Son gezdiklerim (cihaz-yerel, kitapyurdu emsali): ürün detay ziyareti
tarayıcı yerel listesine eklenir (en yeni başta, ~10 ürün, tekrar ziyaret öne taşır). Ana sayfada
kişisel feed'in ALTINDA 'Son Gezdiklerim' şeridi; satıştan kalkan/silinen ürün sessizce atlanır.
Login GEREKMEZ; sinyal cihaza bağlı (senkron YOK — kitapyurdu davranışı; hesaba bağlama ayrı
feature). Backend'e gezinme sinyali YAZILMAZ (048 söküm kararı korunur; PostHog bağımsız).
Sinyalsiz boş durum değişmez; son gezdiklerim varsa boş durumda da şerit görünür. Satın alınan
ürün şeritten elenmez (gezilen şey hatıradır)."

**Kademe**: Küçük — yeni aggregate/tablo/event/servis kontratı yok; sinyal tarayıcıda yaşar,
görünüm mevcut vitrin verisiyle çizilir. Yalnız `spec.md` + `tasks.md` üretilir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gezilen kitaplar ana sayfada şerit olur (Priority: P1)

Kullanıcı (login'li ya da anonim) ürün detay sayfalarını gezdikçe o kitaplar "son gezdiklerim"
listesine girer; ana sayfayı açtığında kişisel feed'in altında (feed yoksa boş durum mesajının
altında) "Son Gezdiklerim" şeridi en son gezilen başta olmak üzere görünür.

**Why this priority**: Feature'ın kendisi; tek başına MVP.

**Independent Test**: Temiz tarayıcıda 3 ürün detayı gez → ana sayfada şeritte 3 kart, en son
gezilen ilk sırada; birini tekrar ziyaret et → o kart başa taşınır.

**Acceptance Scenarios**:

1. **Given** hiç ürün gezmemiş kullanıcı, **When** bir ürün detayına girer ve ana sayfaya döner,
   **Then** şeritte o ürün görünür.
2. **Given** şeridinde A,B,C olan kullanıcı (C en yeni), **When** A'yı tekrar ziyaret eder,
   **Then** sıra A,C,B olur (A başa taşınır, tekrar kaydı yoktur).
3. **Given** 10 ürünlük dolu liste, **When** 11. ürün gezilir, **Then** en eski kayıt düşer
   (liste 10'da kalır).
4. **Given** kullanıcı bir kitabı satın almış, **When** ana sayfayı açar, **Then** satın alınan
   kitap şeritte KALIR (kişisel feed'den farklı — gezilen şey hatıradır).
5. **Given** hiç gezinmesi olmayan kullanıcı, **When** ana sayfayı açar, **Then** şerit HİÇ
   çizilmez (boş şerit başlığı da yok); mevcut boş durum davranışı aynen sürer.

---

### User Story 2 - Liste cihaza özeldir, hesap/login gerekmez (Priority: P2)

Sinyal tarayıcının yerel deposunda yaşar: anonim kullanıcıda da çalışır, aynı kullanıcının farklı
cihazlarında farklı listeler görünür, sunucuya gezinme kaydı yazılmaz.

**Why this priority**: Bilinçli mimari sadelik (kitapyurdu emsali doğrulandı: login'siz iki
cihazda farklı liste). Hesaba bağlama ileride ayrı feature.

**Independent Test**: Anonim pencerede ürün gez → şerit görünür; farklı tarayıcı profilinde ana
sayfa → o listede görünmez; sunucu tarafında gezinme kaydı oluşmadığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** anonim ziyaretçi, **When** ürün gezip ana sayfaya döner, **Then** şerit login'siz
   görünür.
2. **Given** aynı kullanıcı iki farklı tarayıcı/cihaz, **When** yalnız birinde gezinir, **Then**
   diğerinde şerit değişmez (senkron yok — kabul edilen davranış).
3. **Given** kullanıcı gezinirken, **When** sunucu tarafı incelenir, **Then** gezinme sinyali
   hiçbir servise/veritabanına yazılmamıştır (dış analitik bağımsız ve kapsam dışıdır).

---

### User Story 3 - Vitrinden düşen ürün şeridi bozmaz (Priority: P3)

Listedeki bir ürün artık satışta değilse (silinmiş/satıştan kalkmış/eksik veri) şeritte sessizce
atlanır; kullanıcı hata ya da boş kart görmez.

**Why this priority**: Dayanıklılık kenarı; ana akış olmadan da vitrin çalışır.

**Independent Test**: Listede satış-dışı bir ürün varken ana sayfa açılır; şerit kalan geçerli
ürünlerle çizilir, hata görünmez; geçerli ürün kalmadıysa şerit hiç çizilmez.

**Acceptance Scenarios**:

1. **Given** listesinde satış-dışı ürün olan kullanıcı, **When** ana sayfayı açar, **Then** o ürün
   atlanır, kalanlar çizilir, hata mesajı yoktur.
2. **Given** listedeki TÜM ürünler satış dışı, **When** ana sayfa açılır, **Then** şerit hiç
   çizilmez (başlık dahil).

---

### Edge Cases

- Yerel depo silinmiş/bozulmuş (tarayıcı temizliği, geçersiz içerik): şerit çizilmez, sayfa
  hatasız açılır; sonraki gezinme listeyi sıfırdan kurar.
- Gizli pencere: oturum boyunca çalışır, pencere kapanınca doğal olarak kaybolur (kabul edilir).
- Şerit verisi alınamazsa (vitrin geçici erişilemez): şerit sessizce çizilmez; ana sayfanın kalanı
  etkilenmez.
- Ana sayfadaki kişisel feed ile şeritte aynı kitap görünebilir — elenmez (iki bölüm farklı
  anlam taşır: öneri vs hatıra).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ürün detay sayfası ziyareti, o ürünü kullanıcının cihaz-yerel "son gezdiklerim"
  listesine en başa ekler; listedeki mevcut ürün tekrar ziyarette başa taşınır (tekrar kaydı yok).
- **FR-002**: Liste en fazla 10 ürün tutar; taşınca en eski düşer.
- **FR-003**: Ana sayfada, kişisel feed'in (feed yoksa boş durum mesajının) altında "Son
  Gezdiklerim" şeridi çizilir; sıra en yeni gezilen önce.
- **FR-004**: Liste boşsa ya da çizilebilir geçerli ürün yoksa şerit (başlığı dahil) hiç çizilmez;
  054'ün boş durum davranışı değişmez.
- **FR-005**: Şerit login gerektirmez; anonim kullanıcıda da aynı çalışır.
- **FR-006**: Sinyal yalnız cihazda yaşar: gezinme kaydı hiçbir sunucu/veritabanına yazılmaz;
  cihazlar arası senkron yoktur (hesaba bağlama kapsam dışı, ileriki feature).
- **FR-007**: Satışta olmayan (silinmiş/eksik verili) ürün şeritte sessizce atlanır; hata
  gösterilmez.
- **FR-008**: Satın alınmış ürün şeritten elenmez.
- **FR-009**: Bozuk/okunamayan yerel liste sayfayı kırmaz; şerit çizilmez ve sonraki ziyaretle
  liste yeniden kurulmaya başlar.

### Key Entities

- **Son Gezdiklerim Listesi**: Cihaz-yerel, sıralı ürün kimliği listesi (en yeni önce, ≤10);
  tek yazarı ürün detay ziyaretidir; sunucuda karşılığı YOKTUR.
- **Şerit Kartı**: Mevcut vitrin kart verisinin (ad, kapak, fiyat, yazar...) yeniden kullanımı;
  yeni veri üretilmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 3 farklı ürün gezen kullanıcı ana sayfada 3'ünü de doğru sırayla (en yeni önce)
  görür; tekrar ziyaret sırayı günceller (%100 deterministik).
- **SC-002**: Anonim kullanıcıda şerit login istemeden çalışır; gezinme sırasında sunucu tarafına
  hiçbir gezinme kaydı yazılmaz (0 kayıt).
- **SC-003**: Satış-dışı ürün içeren listeyle ana sayfa hatasız açılır; geçersiz kayıtlar
  kullanıcıya hiç görünmez.
- **SC-004**: Şerit eklendikten sonra ana sayfanın 054 davranışları (kişisel feed, boş durum,
  kaldırılan vitrin öğeleri) regresyonsuz sürer.

## Assumptions

- Liste boyutu 10 (kitapyurdu-benzeri kısa şerit); değişirse tek sabit.
- "Gezme" = ürün detay sayfasının açılması; liste/arama kartına bakmak gezme sayılmaz.
- v1'de "listeyi temizle" düğmesi yok (tarayıcı verisi temizleyince sıfırlanır).
- Gizli pencerede kalıcılık pencere ömrüyle sınırlı — kabul edilen doğal davranış.
- Şerit yalnız ana sayfada (v1); detay sayfası altına da koymak ileriki iş.
- Kart verisi mevcut vitrin okumalarından gelir; yeni sunucu verisi/kontratı gerekmez (kademe
  Küçük gerekçesi).