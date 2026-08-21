# Feature Specification: Davranış-Bazlı Kişiselleştirme (Personalization BC)

**Feature Branch**: `042-behavior-personalization`

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "Personalization BC — davranış-bazlı ürün önerisi; gezinti sinyalleri
JSONL davranış loguyla toplanır, Python servisi kendi DB'sine indirir, CF modeli eğitir; UI sonra."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gezinti sinyallerinin kaydı (Priority: P1)

Ziyaretçi (anonim veya login'li) vitrinde gezerken sistem davranış sinyallerini kalıcı kayda alır:
ürün detay görüntüleme, liste/kategori/marka sayfasında gösterilen ürünler (impression), arama
sorgusu, sepete ekleme. Ziyaretçi bundan hiçbir şey hissetmez; sayfa akışı değişmez.

**Why this priority**: Veri olmadan hiçbir öneri üretilemez; tüm zincirin hammaddesi bu kayıttır.

**Independent Test**: Vitrinde gezinti yapılır; her hareketin doğru alanlarla davranış kaydına
düştüğü tek başına doğrulanabilir (model olmadan da değer taşır: veri seti birikmeye başlar).

**Acceptance Scenarios**:

1. **Given** anonim ziyaretçi, **When** ürün detayına girer, **Then** kayıtta AnonymousId + ürün
   bilgisi (Id, marka, kategori, fiyat) + oturum + zaman yer alır; kişisel veri YER ALMAZ.
2. **Given** login'li kullanıcı, **When** ürün detayına girer, **Then** kayıt ek olarak UserId taşır.
3. **Given** ziyaretçi liste sayfası açar, **When** sayfa render edilir, **Then** gösterilen ürün
   Id listesi tek impression kaydı olarak düşer.
4. **Given** ziyaretçi arama yapar, **When** sonuçlar döner, **Then** arama metni kayda düşer.
5. **Given** ziyaretçi sepete ürün ekler, **When** işlem tamamlanır, **Then** sepete-ekleme kaydı düşer.
6. **Given** kayıt mekanizması arızalı, **When** ziyaretçi gezinir, **Then** sayfalar normal çalışır
   (kayıt kaybı kabul edilir, alışveriş akışı asla etkilenmez).

---

### User Story 2 - Sinyallerin Personalization deposuna aktarımı (Priority: P2)

Personalization servisi biriken davranış kayıtlarını periyodik okur ve kendi kalıcı deposuna işler.
Aynı kayıt iki kez okunsa da depoda tek satır oluşur (idempotent aktarım).

**Why this priority**: Eğitim veri setini oluşturur; P1 olmadan anlamsız, P3'ün önkoşulu.

**Independent Test**: Örnek davranış kayıtları üretilir; servisin bunları eksiksiz, tekrarsız ve
şemaya uygun biçimde kendi deposuna indirdiği tek başına doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** birikmiş davranış kayıtları, **When** aktarım çalışır, **Then** tüm geçerli satırlar
   depoda görünür; alanlar kayıptaki değerlerle birebir aynıdır.
2. **Given** aktarım ikinci kez aynı kaynağı okur, **When** işlem biter, **Then** çift kayıt oluşmaz.
3. **Given** bozuk/bilinmeyen şemalı satır, **When** aktarım çalışır, **Then** satır atlanır ve
   atlanan sayısı raporlanır; aktarım durmaz.
4. **Given** Personalization servisi kapalı, **When** ziyaretçiler gezinmeye devam eder, **Then**
   kayıtlar birikir; servis açılınca kaldığı yerden işlenir.

---

### User Story 3 - Model eğitimi ve öneri sorgusu (Priority: P3)

Zamanlanmış eğitim süreci depodaki etkileşimlerden öneri modeli üretir. Öneri sorgu ucu, kullanıcı
kimliği (UserId veya AnonymousId + oturumda gezilen ürünler) karşılığında en uygun N ürünü döner;
model yoksa ya da kullanıcı tanınmıyorsa "en popüler" listesiyle yanıt verir — asla boş dönmez.
Ekranda gösterim bu feature'ın KAPSAMI DIŞINDADIR; sorgu ucu doğrulama/gelecek-tüketici yüzeyidir.

**Why this priority**: Zincirin çıktısı; P1+P2 verisi olmadan çalışamaz.

**Independent Test**: Depoya bilinen etkileşim seti konur; eğitim koşulur; sorgu ucunun etkileşimli
kullanıcıya kişiselleştirilmiş, tanınmayana popüler liste döndürdüğü doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** yeterli etkileşim verisi, **When** eğitim çalışır, **Then** yeni model üretilir ve
   sorgular kesintisiz yeni modele geçer.
2. **Given** etkileşimli kullanıcı, **When** öneri sorgulanır, **Then** kendi geziniminden türeyen,
   popüler listeden farklılaşabilen top-N ürün Id listesi döner.
3. **Given** hiç verisi olmayan/tanınmayan kimlik, **When** öneri sorgulanır, **Then** popüler
   fallback listesi döner; yanıt boş olmaz.
4. **Given** henüz hiç model eğitilmemiş, **When** öneri sorgulanır, **Then** popüler fallback döner.

---

### Edge Cases

- İlk ziyarette AnonymousId çerezi yoksa oluşturulur; sonraki kayıtlar aynı kimlikle devam eder.
- Kayıt dosyası rotasyona girerken aktarım çalışırsa satır kaybı/çift okuma olmaz (idempotentlik).
- Depoda ürün sayısı çok azken (soğuk başlangıç) sorgu yine popüler fallback ile yanıt verir.
- Eğitim sırasında sorgu gelirse eski model yanıt vermeye devam eder (kesinti yok).
- Login'li kullanıcının önerisinde yalnız kendi etkileşimleri etkilidir; başka kullanıcının kimliğine
  bağlı veri sorgu yanıtında sızmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem ürün detay görüntülemeyi davranış kaydına almalı; ürün alanları (Id, marka,
  kategori, fiyat) kayıt anında kaydın içinde taşınmalı (sonradan başka kaynağa bakılmaz).
- **FR-002**: Sistem liste/kategori/marka sayfalarında gösterilen ürün Id listesini tek impression
  kaydı olarak almalı.
- **FR-003**: Sistem arama sorgusu metnini davranış kaydına almalı.
- **FR-004**: Sistem sepete ekleme eylemini davranış kaydına almalı.
- **FR-005**: Her kayıt oturum kimliği, kanal, zaman damgası ve şema sürümü taşımalı; anonim
  ziyaretçide AnonymousId, login'li kullanıcıda ek olarak UserId bulunmalı.
- **FR-006**: Davranış kaydı şeması sabit kontrat olmalı: EventType, Channel, UserId?, AnonymousId,
  ProductId?, Brand?, Category?, Price?, SearchTerm?, ShownProductIds?, SessionId, Timestamp,
  SchemaVersion.
- **FR-007**: Davranış kaydına kişisel veri (ad, e-posta, adres, demografi) YAZILMAMALI.
- **FR-008**: Davranış kaydı alma hattı alışveriş akışını bloklamamalı; kayıt hatası sayfayı düşürmemeli.
- **FR-009**: Personalization servisi kayıtları periyodik okuyup kendi deposuna idempotent işlemeli;
  tekrar okuma çift satır üretmemeli.
- **FR-010**: Geçersiz/bilinmeyen şemalı satır atlanmalı ve atlanan adet gözlemlenebilir olmalı;
  aktarım süreci durmamalı.
- **FR-011**: Zamanlanmış eğitim süreci depodaki etkileşimlerden öneri modeli üretmeli; yeni model
  sorgu kesintisi olmadan devreye girmeli.
- **FR-012**: Öneri sorgu ucu UserId veya AnonymousId + oturumda gezilen ürün listesi ile çağrılmalı
  ve top-N ürün Id listesi dönmeli.
- **FR-013**: Model yokken, kullanıcı tanınmıyorken veya kişisel sonuç üretilemiyorken sorgu "en
  popüler" fallback listesi dönmeli; yanıt hiçbir durumda boş olmamalı.
- **FR-014**: Personalization deposuna yalnız Personalization servisi erişmeli; başka hiçbir servis
  bu depoya bağlanmamalı.
- **FR-015**: Davranış verisinin tek kalıcı sahibi Personalization deposu olmalı; taşıma katmanındaki
  kayıtlar geçici kabul edilmeli.

### Key Entities

- **BehaviorEvent**: Tek davranış kaydı; FR-006 şemasındaki alanları taşır. Depodaki ham eğitim satırı.
- **Session**: SessionId ile gruplanan gezinti dizisi; anonim oturum-içi önerinin bağlamı.
- **TrainedModel**: Eğitim çıktısı + üretim zamanı/veri aralığı meta bilgisi; sorguların kaynağı.
- **RecommendationResult**: Sorgu yanıtı; sıralı ürün Id listesi + kaynağı (kişisel / popüler fallback).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Vitrinde yapılan gezinti eylemlerinin tamamı 1 dakika içinde Personalization deposunda
  sorgulanabilir durumda olur.
- **SC-002**: Etkileşim geçmişi olan kullanıcıya yapılan öneri sorgusu, geçmişi olmayan kullanıcıya
  dönen listeden farklılaşabilen kişisel bir liste döner; geçmişsiz kimlik her zaman popüler liste alır.
- **SC-003**: Öneri sorgusu 500 ms altında yanıtlanır.
- **SC-004**: Davranış kayıtlarının hiçbirinde kişisel veri (ad, e-posta, adres, demografi) bulunmaz.
- **SC-005**: Personalization servisi tamamen kapalıyken alışveriş deneyimi (gezinti, sepet, sipariş)
  hiçbir ölçülebilir biçimde etkilenmez.
- **SC-006**: Aynı kayıt kaynağı iki kez işlense de depodaki satır sayısı değişmez (%0 çift kayıt).

## Assumptions

- Karar (tasarım oturumu, 2026-08-21): taşıma katmanı ayrı kategorili, versiyonlu JSONL davranış log
  dosyasıdır; integration event / mesaj kuyruğu bilinçli olarak EKLENMEZ (ikinci tüketici doğarsa
  event'e terfi). Anayasa I'in kanal listesine göre plan aşamasında değerlendirme/amendment gerekir.
- Karar: Personalization BC Python servisidir (FastAPI); Aspire resource'u olarak koşar ve
  `personalizationDb`'nin tek sahibidir. Sistemdeki ilk .NET-dışı BC'dir.
- Karar: MVP modeli pozitif-yalnız collaborative filtering (implicit ALS) + popüler fallback;
  impression verisi toplanır ama impression-bazlı özellikli model sonraki fazdadır.
- Yakalama noktası WebApp sunucu tarafıdır (tüm kullanıcı eylemlerinin hunisi); tarayıcı-tarafı
  izleme (JS beacon) yoktur. ChatAgent kanalından dönen eylemler bu huniden geçmez (bilinen boşluk).
- Dev ortamı tek makinedir; dosya tabanlı taşıma bu varsayıma yaslanır. Kanal alanı şimdilik hep "web".
- Veri hacmi küçüktür; hedef model kalitesi değil, uçtan uca çalışan doğru pipeline'dır.
- Ekranda gösterim ("Sana önerilenler" şeridi) bu feature'da YOKTUR; ayrı feature olarak ele alınacak.
- Kapsam dışı: mobil ingest, identity stitching, demografi (yaş/cinsiyet profil genişletmesi ayrı
  feature), LLM zevk profili, ChatAgent MCP tool'u, A/B testi, satın alma sinyali (Order event'i sonra).
- Sayfa görüntüleme başına canlı LLM çağrısı yapılmaz.
- Mevcut .NET servislerinde değişiklik yoktur; yalnız WebApp (kayıt alma) ve AppHost (yeni resource)
  değişir.