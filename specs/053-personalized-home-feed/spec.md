# Feature Specification: Kişiselleştirilmiş Ana Sayfa — Çoklu-Kuşak Öneri (Python Beyin + .NET Serving)

**Feature Branch**: `053-personalized-home-feed`

**Created**: 2026-08-30

**Status**: Draft (mimari kilitlendi 2026-08-31)

**Input**: WebApp ana sayfasındaki statik "öne çıkan kitaplar" vitrini kaldırılıp yerine kullanıcının davranış
geçmişine göre kişiselleşen YouTube-tarzı çoklu-kuşak öneri akışı gelecek. Kişiselleştirme **beyni ayrı bir
Python mikroservisidir**: sinyalleri kendi deposunda toplar, kullanıcının zevk profilini (öznitelik + ağırlık +
oran) üretir. **Ürün sıralaması (ranking) .NET Storefront'ta** kalır (katalog sahibi). Faz-1 profil içerik-tabanlı
sezgiseldir (yazar/kategori facet ağırlığı); gerçek model eğitimi (NLP/embedding, CF) ve semantik arama sonraki fazlar.

## Ölçek Kademesi

**Tam** (yeni Python mikroservisi + yeni DB + yeni integration event + yeni sinyal tipi + servisler-arası okuma +
mevcut `Personalization.Api` emekliye ayrılır). Tam spec-kit akışı işletilir.

## Mimari Özet (bağlam)

Öneri hesabı iki sorumluluğa bölünür ve iki teknoloji yığınında yaşar:

- **Beyin — Python mikroservisi (`reco_trainer`):** Davranış + satın-alma sinyallerini kendi Postgres deposunda
  (feature store) toplar; kullanıcının **zevk profilini** üretir = ilgi kümeleri + öznitelik ağırlıkları + oran +
  "neden" etiketi. **Ürün kimliği (bookId) üretmez, katalog sıralaması yapmaz.** Mevcut .NET `Personalization.Api`
  (048) EMEKLİYE ayrılır; Python devralır.
- **Serving — .NET:** WebApp (BFF) profili Python'dan okur; her ilgi kümesini **Storefront**'a verir; Storefront
  kendi read-model'inde **ranking** yapar (aday çek → ağırlıklı-örtüşme skor → çeşitlendir → hidratla) ve sıralı
  kitap kartlarını döndürür. Ana sayfa çoklu-kuşak render edilir.
- **Kanallar:** WebApp gezinme sinyali → Python (HTTP); Order satın-alma `OrderCompleted` → **Storefront**
  (yazar/kategori zenginleştirir) → `PurchaseEnriched` (broker event) → Python. Profil geri
  dönüşü Python → serving (REST). Tümü sanksiyonlu kanal (BC izolasyonu korunur).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Anonim ziyaretçiye baktığı kitaba göre öneri (Priority: P1)

Login olmamış bir ziyaretçi bir kitaba tıklar, sonra ana sayfaya döner. Ana sayfa artık statik vitrin değildir:
tıkladığı kitabın yazar/kategori özniteliklerine göre benzer kitapların yer aldığı kişisel kuşaklar gösterir.

**Why this priority**: Feature'ın çekirdeği. Anonim akış çalışmadan hiçbir şey çalışmaz; kullanıcıların büyük
kısmı login olmadan gezer. Ana sayfa "herkese aynı"dan "sana göre"ye geçer.

**Independent Test**: Temiz oturumda bir kitaba tıkla, ana sayfaya dön; o kitabın yazar/kategorisiyle örtüşen
kuşakların çıktığı doğrulanır. Sinyal (WebApp→Python) → profil (Python) → ranking (Storefront) → render uçtan uca, login gerekmez.

**Acceptance Scenarios**:

1. **Given** yeni anonim ziyaretçi (hiç sinyal yok), **When** ana sayfayı açar, **Then** kişisel kuşak yerine
   popüler/puana göre herkese-aynı vitrin gösterilir (soğuk başlangıç).
2. **Given** anonim ziyaretçi bir kitaba tıkladı (ProductViewed sinyali Python'a düştü), **When** ana sayfaya döner,
   **Then** o kitabın yazarı/kategorisiyle örtüşen en az bir kişisel kuşak gösterilir.
3. **Given** anonim ziyaretçi tarayıcıyı kapatıp yeniden açtı, **When** ana sayfaya döner, **Then** önceki gezinme
   geçmişi korunur (kalıcı anonim kimlik) ve öneriler sıfırlanmaz.
4. **Given** anonim ziyaretçi arama yaptı ama sonuca tıklamadı, **When** ana sayfaya döner, **Then** arama zayıf
   sinyal olarak profile katkı verir.

### User Story 2 - Çoklu-ilgi kuşakları + oransal çeşitlilik (Priority: P1)

Birden çok ilgisi olan kullanıcı (ör. çok Tarih, az Rus klasiği) tek baskın türe boğulmaz. Her ilgi ayrı kuşak
alır; slot dağılımı ilgilerin **oranını** yansıtır (baskın hepsini almaz); ek "keşif" kuşağı komşu tür önerir.

**Why this priority**: Balon (echo chamber) sorununun yapısal çözümü. Argmax-sort baskın ilgiyi öne yığar,
azınlığı gömer; oransal (calibrated) dağıtım + çoklu-kuşak feature'ın "YouTube gibi" vaadinin kalbi.

**Independent Test**: İki ilgi kümesi üret (ör. 10 Tarih, 2 Rus), ana sayfada her iki küme için ayrı kuşak +
keşif kuşağı çıktığı, azınlığın taban kotayla korunduğu, dağılımın orantılı olduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** iki ilgi kümesi (baskın + azınlık), **When** ana sayfayı açar, **Then** her küme için en az bir
   kuşak; slot payı ağırlık oranını yansıtır; azınlık taban kotayla korunur (argmax değil).
2. **Given** tek baskın ilgi, **When** ana sayfayı açar, **Then** baskın kuşağa ek en az bir keşif/komşu-tür kuşağı.
3. **Given** bir kuşak içi öneriler, **When** render edilir, **Then** arka arkaya birebir benzer kitaplar tekrarlanmaz (MMR).

### User Story 3 - Login geçmiş dikişi + zengin sinyal ağırlığı (Priority: P2)

Anonimken gezinen kullanıcı login olur. Login öncesi anonim geçmiş kaybolmaz; gezinme + sepet + satın-alma
birleşerek daha isabetli profil oluşur (satın-alma en ağır, arama en hafif; yeni sinyal eskiden ağır).

**Why this priority**: Login anında sıfırlanmama güven verir; zengin sinyal isabeti artırır. P2 çünkü P1 anonim
akış zaten değer üretir.

**Independent Test**: Anonimken sinyal biriktir, login ol; ana sayfanın anon+login geçmişini birleşik profil
yansıttığı; satın-alınan özniteliğin aramadan üste geldiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** anonimken biriken geçmiş, **When** login olur, **Then** öneriler anon geçmişi dahil birleşik profili yansıtır (dikiş).
2. **Given** hem arama hem satın-alma sinyali, **When** profil türetilir, **Then** satın-alınan öznitelik aranandan yüksek ağırlık alır.
3. **Given** çok eski + çok yeni sinyal, **When** profil türetilir, **Then** yeni sinyal eskiden ağır basar (tazelik).

### Edge Cases

- **Hiç sinyal yok**: profil boş → popüler/puan temelli herkese-aynı vitrin (soğuk başlangıç); boş/kırık sayfa yok.
- **Anonim kimlik yenilenirse**: çerez silinir/dolarsa geçmiş kopar → soğuk başlangıç; veri kaybı raporlanmaz.
- **Tek aşırı-baskın ilgi**: sublinear + IDF + oransal taban kota + keşif ile tek türe kilitlenmez.
- **Katalogda karşılığı olmayan ilgi**: profildeki öznitelik için stokta ürün yoksa o kuşak atlanır (boş kuşak render edilmez).
- **Satın-almada öznitelik eksik**: `OrderCompletedItem` yazar/kategori taşımaz → **Storefront** read-model'den
  zenginleştirir; zenginleştirilemeyen kalem o boyutta profile katkı vermez, akışı bozmaz.
- **Beyin (Python) erişilemez**: serving en son bilinen profille ya da soğuk-başlangıçla çalışır; ana sayfa asla boş.
- **Waterfall derin kaydırma**: aday havuzu tükenince tekrara düşmeden zarifçe biter ya da keşif/popülerle doldurulur.

## Requirements *(mandatory)*

### Functional Requirements

**Kullanıcı deneyimi (serving)**

- **FR-001**: Sistem ana sayfada statik "öne çıkan kitaplar" vitrinini KALDIRMALI, yerine çoklu-kuşak öneri akışı sunmalı.
- **FR-002**: Feed, kullanıcının zevk profilinden türeyen kuşaklar göstermeli; profil öznitelik bazlı (en az yazar, kategori; mümkünse dönem).
- **FR-007**: Feed birden çok ilgi kümesini AYRI kuşaklar olarak göstermeli; tek ortalamada birleştirmemeli.
- **FR-008**: Feed her ilgi kümesine en az bir kuşak (taban kota) garanti etmeli — azınlık ilgi gömülmemeli.
- **FR-009**: Feed en az bir "keşif" kuşağı içermeli — baskın ilgi dışında komşu/farklı öneri (balon kırma).
- **FR-010**: Bir kuşak içindeki öneriler arka arkaya birebir benzer kalemleri tekrarlamamalı (çeşitlendirme/MMR).
- **FR-011**: Hiç profil yoksa sistem soğuk-başlangıca düşmeli (popüler/puan), boş/kırık sayfa göstermemeli.
- **FR-014**: Öneri akışı sayfalanabilir olmalı (waterfall); kaydırdıkça daha fazla öneri yüklenmeli.
- **FR-018**: Her kuşak "neden gösterildiği"ni ifade eden gerekçe taşımalı (ör. "X yazarına baktığın için").
- **FR-025**: Öneri dağıtımı **oransal (calibrated)** olmalı — slot payı ilgi ağırlıklarının oranını yansıtmalı;
  en yüksek ağırlık tüm feed'i almamalı (argmax-sort değil).

**Sinyal toplama (ingest → Python)**

- **FR-003**: Sistem arama eylemini de sinyal olarak kaydetmeli (yeni tür), sorgu + eşleşen öznitelik (kategori/yazar) ile.
- **FR-012**: Sistem anonim geçmişi kalıcı anonim kimlikle korumalı; tarayıcı kapanıp açıldığında sıfırlanmamalı.
- **FR-013**: Login olunca sistem login-öncesi anonim geçmişi login kullanıcısıyla birleştirmeli (dikiş, okuma/türetim anı); login anında sıfırlanmamalı.
- **FR-019**: Satın-alma sinyali öznitelik (yazar/kategori) taşımıyorsa, **Storefront** katalog read-model'inden
  zenginleştirip beyne `PurchaseEnriched` (broker event) ile iletmeli; gezinme WebApp'te toplama-anında zenginleşir. Beyin katalogu bilmez.

**Beyin — profil türetimi (Python)**

- **FR-004**: Profil türetimi sinyal-türü önceliğini yansıtmalı: satın-alma > sepet > tıklama > arama.
- **FR-005**: Profil türetimi TAZELİĞİ yansıtmalı — yeni sinyaller eskiden ağır basmalı.
- **FR-006**: Profil türetimi baskın ilgiyi orantısız ezmekten kaçınmalı (yumuşatma/sublinear) ve herkeste-yaygın
  öznitelikleri kişisel-bilgi değeri düşük sayarak kırmalı (nadirlik/IDF, kendi korpusundan).
- **FR-023**: Beyin çıktısı = zevk profili (ilgi kümeleri + sıralı öznitelik ağırlıkları + oran + gerekçe).
  Beyin **ürün kimliği (bookId) üretmez** ve katalog sıralaması yapmaz.
- **FR-026**: Beyin kendi veri deposuna (feature store) sahip olmalı; sinyalleri sanksiyonlu kanaldan (HTTP telemetri
  + broker event) almalı, başka BC'nin veritabanına doğrudan erişmemeli.

**Serving — ürün sıralaması (Storefront, .NET)**

- **FR-016**: Ürün sıralaması (bu zevke hangi kitaplar, hangi sırayla) katalog read-model'inin sahibinde
  (Storefront) yapılmalı; profil türetiminden (beyin) ayrı sorumluluk olmalı.
- **FR-027**: Ranking, beyinden gelen öznitelik ağırlıklarını kendi read-model'iyle eşleştirmeli (aday seçimi +
  ağırlıklı-örtüşme skor + MMR çeşitlendirme + stok/satış filtresi + tekrar önleme); tercih türetmemeli.
- **FR-015**: Öneri hesaplama mantığı kullanıcı arayüzünde değil servis sınırlarında yaşamalı; arayüz (WebApp BFF) yalnız okumaları bağlamalı.

**Kararlılık + sınır**

- **FR-017**: Beyin çıktı sözleşmesi (kümeler + ağırlık + oran + gerekçe) faz-1 içerik-tabanlı mantıktan bağımsız
  SABİT kalmalı; sonraki fazda içi (NLP/embedding, CF) değişse de sözleşme aynı kalmalı.
- **FR-024**: Servisler-arası iletişim yalnız integration event (broker) + sanksiyonlu REST okuma ile olmalı;
  imperatif agent/MCP yolu kullanılmamalı. Mevcut `Personalization.Api` (048) emekliye ayrılmalı, Python devralmalı.

### Faz-1 Teslim Sıralaması (ince dikey, iki adım)

- **1a — veri hattı**: Python mikroservisi + ingest (WebApp gezinme HTTP + Order/Catalog event) + feature store'a
  yazma (CRUD). Doğrulama: sinyal Python DB'de görünür. (Serving değişmez.)
- **1b — profil + serving**: basit agregasyon profil (beyin) + Storefront ranking slice + WebApp çoklu-kuşak feed.
  Doğrulama: ana sayfa kişiselleşir (US1). Her adım bağımsız doğrulanabilir.

### Out of Scope (Sonraki Fazlar — Roadmap)

- **Faz-2 — NLP/embedding öneri**: kitap metni → vektör (sentence-transformer/torch), semantik benzerlik,
  **pgvector** vektör deposu. Gerçek model eğitimi burada; Python ML iş yükü. Ayrıca **collaborative filtering**
  (user×item, "senin gibiler") = ayrı opsiyonel eksen (kalabalık sinyali; NLP değildir).
- **Faz-3 — semantik arama (kullanıcı hayali)**: kullanıcı serbest metin girer → aynı embedding uzayında
  semantik retrieval → sonuç. "Metni gir, arka taraf dönsün" (pgvector sorgu tarafı).
- **Online/incremental (event-başına) profil** ve eşik-tetikli işlem (faz-1 = basit agregasyon / sonra zamanlanmış).
- **Ayrı tam-metin/arama altyapısı servisi** (mevcut arama yeterli; tetik = arama kutusu kalitesi aşınca).

### Key Entities *(include if data involved)*

- **Davranış Sinyali**: Kullanıcının tek eylemi (görüntüleme, sepete ekleme, arama). Kim (anonim/kullanıcı kimliği),
  hangi ürün/öznitelik (yazar, kategori, fiyat), ne zaman. Gezinme toplama-anında zenginleşir. Python feature store'da tutulur.
- **Satın-Alma Sinyali**: Tamamlanmış siparişin kalemleri; en güçlü niyet. Öznitelik eksikse Storefront read-model'den zenginleştirir (`PurchaseEnriched`).
- **Feature Store**: Beynin kendi deposu; sinyaller sanksiyonlu kanaldan birikir. Türev/tekrar-kurulabilir.
- **Zevk Profili (beyin çıktısı)**: Sinyallerden türetilmiş, ağırlıklı + oranlı öznitelik koleksiyonu (yazar/
  kategori/dönem → ağırlık), ilgi kümelerine bölünmüş + gerekçe. Sözleşmesi SABİT (FR-017).
- **Öneri Kuşağı**: Ana sayfada tek ilgiye (ya da keşfe) karşılık gelen, sıralı kitap kartları + başlık/gerekçe.
  Katalog-sıralaması Storefront'ta üretilir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bir kitaba tıklayıp ana sayfaya dönen anonim ziyaretçi, profil oluşunca o tıklamanın yansımasını
  (ilgili yazar/kategori kuşağı) görür — kişisel kuşak, statik vitrin değil.
- **SC-002**: Birden çok ilgisi olan kullanıcıda ana sayfa her ilgi için ayrı kuşak gösterir; azınlık ilgi
  (toplam sinyalin ≥%10'u) en az bir kuşakla temsil edilir; dağılım oransaldır.
- **SC-003**: Ana sayfada baskın ilgi dışından en az bir keşif kuşağı bulunur; feed tek türe %100 kilitlenmez.
- **SC-004**: Anonim ziyaretçi tarayıcıyı kapatıp açtığında önceki geçmişten türeyen öneriler korunur (sıfırlanma %0).
- **SC-005**: Login olan kullanıcının login-öncesi anonim geçmişi profile dahil edilir; öneriler anon geçmişi yok saymaz.
- **SC-006**: Ana sayfa ilk ekranı algılanır gecikme yaratmadan yüklenir; profil beyin ayrı serviste tutulur, serving REST'le okur. Faz-1 profil istek-anında türetilir (tek-kullanıcı `Signal` GROUP BY, ucuz); gecikme aşılırsa precompute/cache faz-2'de eklenir.
- **SC-007**: Kullanıcı aşağı kaydırdıkça kesintisiz yeni öneri yüklenir; aynı kitap kısa aralıkta tekrarlanmaz.
- **SC-008**: Faz-1 1a sonrası sinyaller Python feature store'da doğrulanabilir; 1b sonrası ana sayfa kişiselleşir (US1 uçtan uca).

## Assumptions

- **Beyin ayrı Python mikroservisi + ayrı DB**: `reco_trainer` kendi Postgres feature store'una sahip; sinyalleri
  broker event + WebApp HTTP telemetriyle alır (BC'nin DB'sine erişmez). Gerekçe = sektöre-yönelik ML-ops + Python/ML öğrenme (kalıcı kanaat).
- **Mevcut `Personalization.Api` (048) emekliye ayrılır**: Python devralır (sinyal toplama + profil). 048'in
  değeri = ingest deseni + `OrderCompleted` sözleşmesi; Python bunları devralır. Churn bilinçli.
- **Ranking .NET Storefront'ta**: Storefront read-model kitap künyesini (yazar, kategori, görsel, fiyat, puan,
  stok) sunar; profil→bookId eşlemesi + skor + çeşitlendirme + hidrasyon burada.
- **Anonim kimlik altyapısı hazır**: kalıcı `pz_aid` + satırın anon+kullanıcı kimliğini taşıması mevcut; dikiş türetim/okuma anında birleştirmeyle (yeni yazım yok).
- **Satın-alma enrichment**: `OrderCompletedItem` yazar/kategori taşımaz; **Storefront** `OrderCompleted`'ı tüketip read-model'inden zenginleştirir, `PurchaseEnriched` yayar; beyin bunu tüketir (katalogu bilmez).
- **"Dönem" fırsatçı**: künyede varsa profile katılır; yoksa yazar+kategori ile faz-1 çalışır.
- **Async tazelik gecikmesi kabul**: profil periyodik/agregasyonla güncellendiğinden en taze sinyal küçük gecikmeyle yansıyabilir; gerçek-zaman garantisi faz-1 hedefi değil.
- **BC izolasyonu korunur**: servisler-arası tek kanal = integration event (broker) + sanksiyonlu REST okuma;
  .NET yayıncı outbox, Python tüketici idempotent (inbox). Python idiomatik + disiplinli yazılır (`docs/python-conventions.md`).
