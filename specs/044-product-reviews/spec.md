# Feature Specification: Ürün Yorumları ve Puanlama (Reviews)

**Feature Branch**: `044-product-reviews`

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "Ürün yorumları ve puanlama (Reviews) — müşteri güveni için ürün sayfasında
yorum + yıldız. Satın-alma şartlı (model 2, verified purchase); yorum dilenme yok; izole Reviews BC;
rating özeti event'le Storefront'a denormalize; 1-5 tam yıldız; nop ProductReview referans (kopya değil)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Satın alan müşteri yorum bırakır (Priority: P1)

Ürünü satın alıp siparişi tamamlanmış müşteri, ürün detay sayfasından 1-5 yıldız puan ve
isteğe bağlı metinle yorum bırakır. Satın almamış kullanıcı yorum formunu göremez/gönderemez.

**Why this priority**: Güvenin kaynağı doğrulanmış yorumdur; şartsız yorum spam ve sahte
değerlendirme kapısı açar. Yazma yolu olmadan diğer story'lerin verisi doğmaz.

**Independent Test**: Completed siparişi olan kullanıcıyla yorum gönder → kayıt oluşur;
siparişi olmayan kullanıcıyla dene → engellenir. Elle sipariş verisiyle bağımsız test edilir.

**Acceptance Scenarios**:

1. **Given** kullanıcının o ürünü içeren Completed siparişi var ve ürüne yorumu yok,
   **When** 4 yıldız + metinle gönderir, **Then** yorum kaydedilir ve detayda görünür.
2. **Given** kullanıcının o ürünle ilgili hiç siparişi yok, **When** yorum göndermeyi dener,
   **Then** istek reddedilir ve neden ("satın alanlar yorumlayabilir") bildirilir.
3. **Given** siparişi var ama Completed değil (Pending/Cancelled), **When** yorum dener,
   **Then** reddedilir.
4. **Given** kullanıcı aynı ürüne daha önce yorum bırakmış, **When** ikinci yorum dener,
   **Then** reddedilir (ürün başına tek yorum).
5. **Given** giriş yapılmamış, **When** ürün detayına bakılır, **Then** yorum formu görünmez;
   yorum listesi herkese açıktır.

---

### User Story 2 - Ziyaretçi yorumları ve özeti görür (Priority: P2)

Herkes (girişsiz dahil) ürün detayında yıldız ortalamasını, toplam yorum sayısını ve
yorum listesini (en yeni üstte, sayfalı) görür. Yorumsuz üründe bölüm sade bir
"henüz yorum yok" durumu gösterir.

**Why this priority**: Yorumun değeri okunmasında; yazma yolu (US1) veri üretir, bu story
güven etkisini müşteriye taşır.

**Independent Test**: Elle eklenmiş yorumları olan ürünün detayında liste + ortalama doğru;
yorumsuz üründe boş durum. US1'den bağımsız, tohum veriyle test edilir.

**Acceptance Scenarios**:

1. **Given** üründe 3 yorum (5,4,3), **When** detay açılır, **Then** ortalama 4,0 ve "3 yorum"
   görünür; yorumlar en yeni üstte sayfalı listelenir.
2. **Given** üründe hiç yorum yok, **When** detay açılır, **Then** yıldız özeti çizilmez,
   "henüz yorum yok" görünür.
3. **Given** yorum sahibi adı, **When** liste çizilir, **Then** ad maskeli görünür (ör. "H** D**");
   her yorumda "doğrulanmış alışveriş" rozeti vardır.

---

### User Story 3 - Vitrin kartında yıldız özeti (Priority: P3)

Ürün liste/vitrin kartlarında yıldız ortalaması + yorum adedi görünür; yorumsuz üründe
rozet çizilmez. Özet, yorum eklendikçe kısa gecikmeyle güncellenir.

**Why this priority**: Karar anı listede başlar; özet karta taşınmazsa yorumun dönüşüm
etkisi sınırlı kalır. Detay (US2) olmadan da vitrin özeti tek başına değer taşır.

**Independent Test**: Yorumlu ürünün kartında yıldız + adet; yorum ekle → kart özeti
güncellenir; yorumsuz üründe rozet yok.

**Acceptance Scenarios**:

1. **Given** üründe 2 yorum ortalama 4,5, **When** ürün listesi açılır, **Then** kartta
   yıldız ve "(2)" görünür.
2. **Given** ürüne yeni yorum eklendi, **When** kısa süre sonra liste yenilenir,
   **Then** kart özeti yeni ortalama/adedi gösterir.
3. **Given** üründe yorum yok, **When** liste açılır, **Then** kartta yıldız rozeti yoktur.

---

### Edge Cases

- Ürün sonradan vitrinden kalkarsa (Delisted/unpublished) mevcut yorumlar silinmez; ürün
  görünmediği için yüzeyde çıkmaz.
- Aynı ürünü birden çok siparişte alan kullanıcı yine TEK yorum bırakır.
- Sipariş Completed olduktan sonra iptal/iade akışı bugün yok; şart yalnız Completed'a bakar.
- Yorum metni boş olabilir (yalnız yıldız); yıldız zorunlu, 1-5 tam sayı dışındaki değer reddedilir.
- Çok uzun metin makul sınırda kesilir/reddedilir (sınır planda netleşir, kontratta sabitlenir).
- Doğrulama kaynağı (sipariş bilgisi) erişilemezse yazma reddedilir (fail-closed); okuma etkilenmez.
- Uygunluk denetimi ihlal bulduğunda yorum gizlenir ve özet düşer; aynı kullanıcı yeni yorum
  AÇAMAZ (ürün başına tek yorum hakkı kullanılmıştır).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem yorum yazmayı YALNIZ o ürünü içeren Completed siparişi olan kullanıcıya
  izin vermeli; şart sağlanmıyorsa nedenli hata dönmeli.
- **FR-002**: Yorum 1-5 arası tam yıldız puan taşımalı (zorunlu); metin opsiyonel olmalı.
- **FR-003**: Kullanıcı başına ürün başına en fazla BİR yorum olmalı; tekrar deneme reddedilmeli.
- **FR-004**: Yorum listesi herkese açık olmalı (girişsiz dahil), en yeni üstte ve sayfalı.
- **FR-005**: Yorum sahibinin adı maskeli gösterilmeli; her yorum "doğrulanmış alışveriş"
  rozeti taşımalı (şart gereği hepsi doğrulanmıştır).
- **FR-006**: Ürünün puan özeti (ortalama + adet) yorum eklendikçe güncellenmeli ve ürün
  detayı + vitrin kartlarında görünmeli; yorumsuz üründe özet/rozet çizilmemeli.
- **FR-007**: Reviews verisi kendi bağlamında (izole) yaşamalı; ürünü opak kimlikle
  referans etmeli; başka bağlamın verisine doğrudan erişmemeli.
- **FR-008**: Satın-alma doğrulaması sipariş bağlamından sanksiyonlu bir kanalla sorulmalı;
  kanal erişilemezse yazma reddedilmeli (fail-closed), okuma yolu etkilenmemeli.
- **FR-009**: Satın-alma-sonrası yorum daveti (mail/popup/bildirim) YAPILMAMALI.
- **FR-010**: Yorum gönderimi yayın öncesi onay/insan moderasyonu beklememeli; gönderilen
  yorum hemen görünür olmalı (admin onay kuyruğu YOK).
- **FR-011**: Yayınlanan her yorum arka planda uygunluk denetiminden geçmeli (küfür/hakaret/
  kişisel saldırı); ihlalde yorum otomatik GİZLENMELİ (silinmez) ve puan özetinden düşülmeli.
- **FR-012**: Denetim gecikmesi/başarısızlığı yayını etkilememeli (yorum görünür kalır);
  denetim tamamlanamayan yorum tekrar denenmeli (sınırlı retry, sonra hata kuyruğu).

### Key Entities

- **Yorum (Review)**: Bir kullanıcının bir ürüne bıraktığı değerlendirme — puan (1-5),
  opsiyonel metin, maskeli görünecek ad, zaman. Ürünü ve kullanıcıyı opak kimlikle taşır.
- **Puan Özeti (RatingSummary)**: Ürün başına türetilmiş ortalama + adet; vitrin ve detay
  yüzeylerine dağıtılan okunur özet.
- **Satın-alma kanıtı**: "Bu kullanıcı bu ürünü Completed siparişte aldı" gerçeği; sipariş
  bağlamına aittir, Reviews yalnız sorar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Satın almamış kullanıcının yorum yazma girişimlerinin %100'ü engellenir
  (arayüzde form yok + doğrudan istek reddi).
- **SC-002**: Gönderilen yorum, gönderen kullanıcıya anında; ürün detayına ve vitrin kart
  özetine en geç 10 saniye içinde yansır.
- **SC-003**: Ortalama ve adet, üründeki yorumlarla her an birebir tutarlıdır (elle sayımla fark 0).
- **SC-004**: Yorumsuz ürünlerde hiçbir yüzeyde yıldız/özet çizilmez; mevcut vitrin/sepet/sipariş
  akışlarında regresyon 0.
- **SC-005**: Aynı kullanıcı + ürün için ikinci yorum denemelerinin %100'ü reddedilir.

## Assumptions

- İnsan moderasyonu/onay ekranı v1'de yok (FR-010); uygunluk denetimi otomatiktir (FR-011,
  AI tabanlı — kelime listesi Türkçe varyasyonları yakalayamaz). Gizlenen yorum için itiraz
  akışı v1 kapsam dışı.
- Yorum düzenleme ve silme v1'de yok; yanlış yorum ancak ileride eklenecek yönetim yüzeyiyle ele alınır.
- "Satın aldı" tanımı = ürünü içeren en az bir Completed sipariş; iade/iptal sonrası geri alma yok
  (bugün iade akışı da yok).
- Ad maskeleme görüntüleme kuralıdır; ad açık saklanabilir, yüzeye maskeli çıkar.
- Faydalı oy ("bu yorum işime yaradı"), fotoğraflı yorum, satıcı yanıtı kapsam dışı.
- Puan kırılımı (5★ kaç adet histogramı) v1 kapsam dışı; yalnız ortalama + adet.
- nopCommerce `ProductReview` scaffold'u yalnız alan/akıl referansı; model sıfırdan tasarlanır.
