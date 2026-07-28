# Feature Specification: Hibrit Ürün Araması (Filtre + Anlamsal, Sohbet Üzerinden)

**Feature Branch**: `019-hybrid-product-search`

**Created**: 2026-07-28

**Status**: Draft

**Input**: User description: "Şu marka veya şu marka olan, fiyat skalası şunlar arası olan, stok sayısı 2 olan,
fiyatı da şundan az olan ürünleri getir diyebileceğim; ayrıca kış sporlarında kullanabileceğim bir ayakkabı
arıyorum diyebileceğim bir yapı — müşteri hizmetleri sohbeti (ChatAgent) üzerinden, iki tür tek cümlede birleşebilir."

**Kademe**: Tam — yeni tablo/uzantı (vektör deposu), yeni MCP kontratı ve yeni dış bağımlılık (embedding servisi) var.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Yapılandırılmış filtre araması sohbetten (Priority: P1)

Ziyaretçi sohbete "X veya Y markalı, fiyatı 1000-3000 arası, stokta en az 2 olan ürünleri göster" yazar;
asistan kriterlere uyan ürünleri ad, marka, kategori, fiyat, stok ve detay linkiyle listeler.

**Why this priority**: Mevcut verilerle (marka, fiyat, stok zaten vitrinde) hemen değer üretir; anlamsal altyapı olmadan da çalışır.

**Independent Test**: Anlamsal altyapı kapalıyken sohbette filtreli arama yapılır; doğru ürün listesi ve linkler döner.

**Acceptance Scenarios**:

1. **Given** vitrinde farklı marka/fiyat/stokta ürünler var, **When** "A veya B marka, 1000-3000 arası" denir, **Then** yalnız uyanlar listelenir.
2. **Given** stokta 1 adet kalmış ürün var, **When** "stokta en az 2 olsun" denir, **Then** o ürün sonuçta yer almaz.
3. **Given** kriterlere uyan ürün yok, **When** arama yapılır, **Then** asistan sonuç bulunamadığını söyler; hata değil.
4. **Given** kullanıcı hiçbir kriter vermeden "ürünleri getir" der, **When** asistan aramayı çalıştırır, **Then** en az bir kriter istenir.

---

### User Story 2 - Anlamsal arama sohbetten (Priority: P2)

Ziyaretçi "kış sporlarında kullanabileceğim bir ayakkabı arıyorum" yazar; asistan adında "kış" geçmese bile
açıklaması/kategorisi anlamca uyan ürünleri (ör. kar botu, kayak ayakkabısı) benzerlik sırasıyla listeler.

**Why this priority**: Feature'ın ayırt edici değeri; ancak filtre araması (US1) olmadan da tek başına anlamlıdır.

**Independent Test**: Açıklamasında ilgili kavramlar geçen ürünler beslenir; doğal dil sorgusuyla anlamca uyanların döndüğü görülür.

**Acceptance Scenarios**:

1. **Given** açıklaması "kar ve buzda kaymaz taban" olan bot var, **When** "kış sporu ayakkabısı" aranır, **Then** bot sonuçlarda üst sıradadır.
2. **Given** sorguyla anlamca alakasız ürünler var, **When** anlamsal arama yapılır, **Then** benzerlik eşiği altındakiler listeye girmez.
3. **Given** bir ürünün anlamsal verisi henüz üretilmemiş, **When** anlamsal arama yapılır, **Then** o ürün sıralamaya girmez; arama hatasız çalışır.

---

### User Story 3 - Hibrit arama: anlamsal + filtre tek cümlede (Priority: P3)

Ziyaretçi "kış sporları için ayakkabı, 3000 TL altı, stokta olsun" yazar; asistan anlamsal eşleşmeyi
fiyat ve stok filtreleriyle birlikte uygular, tek tutarlı liste döner.

**Why this priority**: US1 + US2'nin bileşimi; ikisi bittikten sonra düşük ek maliyetle en doğal deneyimi tamamlar.

**Independent Test**: Anlamca uyan ama fiyatı yüksek bir ürün beslenir; hibrit sorguda listede olmadığı görülür.

**Acceptance Scenarios**:

1. **Given** anlamca uyan 5000 TL'lik ve 2500 TL'lik ürünler var, **When** "3000 altı kış sporu ayakkabısı" denir, **Then** yalnız 2500'lük listelenir.
2. **Given** anlamca uyan ürün stokta 0, **When** "stokta olsun" filtresiyle aranır, **Then** ürün listede yer almaz.

---

### User Story 4 - Ürün verisi değişince anlamsal veri güncel kalır (Priority: P4)

Tedarikçi feed'inden gelen ürün eklendiğinde/değiştiğinde ürünün anlamsal arama verisi otomatik üretilir;
metin değişmediyse gereksiz üretim yapılmaz.

**Why this priority**: US2/US3'ün sürekliliğini sağlar; kullanıcıya görünmez ama arama tazeliği buna bağlıdır.

**Independent Test**: Yeni ürün beslenir; kısa süre içinde anlamsal aramada bulunur. Aynı feed tekrar beslenir; yeni üretim olmaz.

**Acceptance Scenarios**:

1. **Given** yeni ürün feed'den geldi, **When** vitrin satırı oluştu, **Then** ürün anlamsal aramada bulunabilir hale gelir.
2. **Given** ürünün yalnız stok adedi değişti, **When** vitrin satırı güncellendi, **Then** anlamsal veri yeniden üretilmez.
3. **Given** anlamsal veri üretimi geçici olarak başarısız, **When** ürün değişikliği işlenir, **Then** vitrin satırı yine kaydedilir.

### Edge Cases

- Embedding servisi arama anında erişilemezse: anlamsal arama hata Result'ı döner; asistan filtreli aramayı önerebilir.
- MinPrice > MaxPrice gibi tutarsız aralık: doğrulama hatası Result'ı döner; sonuç uydurulmaz.
- Bilinmeyen marka adı verilirse: o marka hiçbir satıra uymaz; kalan kriterlere göre sonuç (veya bulunamadı) döner.
- MaxResults üst sınırın (20) üstünde istenirse üst sınıra kırpılır; hiç verilmezse varsayılan (8) kullanılır.
- Satılamaz satırlar (fiyatı oluşmamış, silinmiş) hiçbir arama türünde sonuçlara giremez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem sohbet asistanına vitrin üzerinde ürün araması yapan tek bir araç sunmalı; araç anonim (girişsiz) çağrılabilmeli.
- **FR-002**: Araç şu opsiyonel kriterleri almalı: marka listesi (OR), MinPrice, MaxPrice, MinStock, serbest metin (anlamsal), MaxResults.
- **FR-003**: En az bir kriter zorunlu; hiçbiri verilmezse doğrulama hatası Result'ı dönmeli.
- **FR-004**: Marka listesi verildiğinde satır, listedeki markalardan herhangi birine uyarsa eşleşmeli (OR birleşimi).
- **FR-005**: MinStock "stokta en az N adet" olarak yorumlanmalı; "stokta olsun" MinStock=1'e karşılık gelir.
- **FR-006**: Serbest metin verildiğinde sonuçlar anlamsal benzerliğe göre sıralanmalı; benzerlik eşiği altındakiler elenmeli.
- **FR-007**: Serbest metin ve filtreler birlikte verildiğinde filtreler kesin (hard) uygulanmalı, sıralama anlamsal benzerlikle yapılmalı.
- **FR-008**: Serbest metin verilmediğinde arama yalnız filtrelerle, deterministik bir sırayla çalışmalı.
- **FR-009**: Sonuç en fazla MaxResults ürün içermeli (varsayılan 8, üst sınır 20); her ürün ad, marka, kategori, fiyat, stok, detay linki taşımalı.
- **FR-010**: Detay linki mevcut ürün arama aracındaki biçimle aynı (gateway-göreli) olmalı.
- **FR-011**: Yalnız satılabilir vitrin satırları aranmalı (fiyatı oluşmuş, silinmemiş) — mevcut vitrin sorgularıyla aynı kural.
- **FR-012**: Ürün vitrin verisi (ad, açıklama, marka, kategori) değişince anlamsal arama verisi otomatik güncellenmeli.
- **FR-013**: Anlamsal veri yalnız arama metni gerçekten değiştiyse yeniden üretilmeli (gereksiz üretim yok).
- **FR-014**: Anlamsal veri üretimi başarısız olsa da vitrin satırı kaydedilmeli; ürün yalnız anlamsal sıralamadan düşmeli.
- **FR-015**: Başarısız üretim, ürünün bir sonraki değişiklik olayında yeniden denenmeli.
- **FR-016**: Boş sonuç bulunamadı (NotFound) Result'ı; beklenen hatalar hata Result'ı olmalı — exception değil.
- **FR-017**: Yeni araç sohbetin hem anonim hem giriş yapmış asistanında kullanılabilir olmalı.
- **FR-018**: Mevcut isim-bazlı ürün arama aracı anonim asistandan kaldırılmalı; giriş yapmış asistanda (sepet akışı için) kalmalı.
- **FR-019**: Anlamsal servis yapılandırması eksikse vitrin servisi açılışta açık bir hatayla durmalı (fail-fast).

### Key Entities

- **Vitrin satırı (StorefrontView)**: mevcut read-model; ad, açıklama, marka, kategori, fiyat, stok. Aramanın tek veri kaynağı.
- **Ürün anlamsal verisi**: ürün başına bir kayıt; arama metninin özeti (hash) ve anlamsal vektörü. Vitrin satırıyla ürün kimliği üzerinden ilişkili.
- **Arama isteği**: opsiyonel kriter kümesi (markalar, fiyat aralığı, asgari stok, serbest metin, sonuç sayısı).
- **Arama sonucu**: eşleşen ürünlerin listesi; her öğe ad, marka, kategori, fiyat, stok ve detay linki taşır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı marka+fiyat+stok bileşimli bir aramayı sohbetten tek mesajla yapar; kriterlere uymayan hiçbir ürün listede görünmez.
- **SC-002**: Adında sorgu kelimesi geçmeyen ama açıklaması anlamca uyan ürün, doğal dil aramasında ilk MaxResults içinde döner.
- **SC-003**: Feed'den gelen yeni ürün, ingestion tamamlandıktan sonra 1 dakika içinde anlamsal aramada bulunabilir.
- **SC-004**: Yalnız stok değişen üründe anlamsal veri üretimi tekrarlanmaz (üretim sayısı değişmez).
- **SC-005**: Anlamsal altyapı erişilemezken filtreli arama %100 çalışmaya devam eder.

## Assumptions

- Arama verisi Storefront read-model'idir; başka bağlamın veritabanına erişilmez (anayasa İlke I).
- Anlamsal vektörler dış embedding servisiyle (mevcut OpenAI aboneliği) üretilir; model: text-embedding-3-small.
- Vektörler vitrin veritabanında saklanır (pgvector uzantısı); AppHost Postgres imajı pgvector destekli imaja geçer.
- Sohbet asistanının vitrin servisine MCP üzerinden bağlanması gerekir; bugün bağlı değildir, bu feature ekler.
- "Stok sayısı 2 olan" ifadesi kullanıcı onayıyla "stokta en az 2" olarak yorumlanmıştır.
- Benzerlik eşiği implementasyonda kalibre edilir; spec yalnız "alakasız sonuç elenir" davranışını şart koşar.
- Arama sonuçları sayfalanmaz; sohbet bağlamında en fazla 20 sonuç yeterlidir.