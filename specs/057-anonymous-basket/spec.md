# Feature Specification: Anonim Sepet + Checkout'ta Login

**Feature Branch**: `057-anonymous-basket`

**Created**: 2026-09-02

**Status**: Draft

**Kademe**: Küçük — tek aggregate (Basket), yeni tablo/şema yok, servisler-arası event yok; merge ucu iç (WebApp→Basket) çağrıdır, dış kontrat saymadık. Yalnız spec.md + tasks.md.

**Input**: User description: "Kullanıcı login olmadan da sepete ürün atabilir olmalı. Satın alınma aşamasında kullanıcının login olması istenecek."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Anonim sepete ekleme (Priority: P1)

Ziyaretçi siteye girer, üye olmadan/giriş yapmadan ürünleri gezer ve sepetine ürün ekler. Sepet sayfasını açar, kalemleri görür, adet değiştirir, kalem siler. Tarayıcıyı kapatıp ertesi gün aynı cihazla dönerse sepeti hâlâ durur.

**Why this priority**: Feature'ın özü. Login zorunluluğu sepete eklemede en büyük vazgeçirme noktası; anonim sepet olmadan diğer hikâyeler anlamsız.

**Independent Test**: Hiç giriş yapmadan ürün ekle, sepet sayfasında gör, adet değiştir, tarayıcı kapat-aç, sepetin durduğunu doğrula.

**Acceptance Scenarios**:

1. **Given** giriş yapmamış ziyaretçi, **When** üründe "Sepete Ekle" der, **Then** ürün sepete eklenir ve login sayfasına YÖNLENDİRİLMEZ.
2. **Given** anonim sepette kalemler var, **When** ziyaretçi sepet sayfasını açar, **Then** kalemleri görür; adet artırma/azaltma ve silme çalışır.
3. **Given** anonim sepette kalemler var, **When** ziyaretçi tarayıcıyı kapatıp aynı cihaz/tarayıcıyla geri gelir, **Then** sepet aynen durur.
4. **Given** ziyaretçi farklı bir cihaz/tarayıcıdan gelir, **When** sepete bakar, **Then** eski cihazın sepetini GÖRMEZ (cihaz-yerel anonim kimlik).

---

### User Story 2 - Checkout'ta login kapısı (Priority: P1)

Anonim ziyaretçi sepetini doldurdu, "Satın Al" der. Sistem giriş ister; ziyaretçi giriş yapar (veya kayıt olur) ve kaldığı yerden checkout'a devam eder — sepeti kaybolmaz.

**Why this priority**: Feature'ın ikinci yarısı; sipariş, ödeme ve adres kullanıcı hesabına bağlı olduğundan satın alma kimliksiz olamaz.

**Independent Test**: Anonim sepet doldur, "Satın Al" de, login ekranının geldiğini ve giriş sonrası aynı kalemlerle checkout sayfasına düşüldüğünü doğrula.

**Acceptance Scenarios**:

1. **Given** anonim sepette kalem var, **When** ziyaretçi "Satın Al" der, **Then** giriş ekranına yönlendirilir.
2. **Given** giriş ekranına yönlendirilen ziyaretçi, **When** başarıyla giriş yapar, **Then** checkout sayfasına döner ve anonim sepetindeki kalemler orada durur.
3. **Given** giriş yapmış kullanıcı, **When** "Satın Al" der, **Then** login sorulmadan doğrudan checkout'a gider (mevcut davranış bozulmaz).

---

### User Story 3 - Login'de sepet birleşmesi (Priority: P2)

Kullanıcının hesabında önceki oturumdan kalma sepet kalemleri var. Anonim gezerken yeni ürünler ekledi, sonra giriş yaptı. İki sepet birleşir: tüm ürünler tek sepette, aynı ürün iki sepette de varsa adetler toplanır.

**Why this priority**: Veri kaybını önler ama ancak US1+US2 çalışırken anlamlı; ilk sürümde "anonim kazanır" bile idare ederdi, birleşme doğru davranış.

**Independent Test**: Hesaplı kullanıcıyla sepete X ekle, çık; anonim X ve Y ekle; giriş yap; sepette X (adetler toplanmış) ve Y'nin durduğunu doğrula.

**Acceptance Scenarios**:

1. **Given** hesap sepetinde X var, anonim sepette Y var, **When** kullanıcı giriş yapar, **Then** sepette X ve Y birlikte görünür.
2. **Given** hesap sepetinde X (2 adet), anonim sepette X (1 adet), **When** giriş yapar, **Then** X'in adedi 3 olur; adet üst sınırı aşılıyorsa üst sınıra sabitlenir.
3. **Given** birleşme tamamlandı, **When** kullanıcı çıkış yapıp anonim gezmeye devam eder, **Then** boş bir anonim sepetle başlar (eski anonim sepet hesaba taşındı, tekrar birleşmez).
4. **Given** hesap sepeti boş, anonim sepette kalemler var, **When** giriş yapar, **Then** anonim sepetin tamamı hesabın sepeti olur.

---

### Edge Cases

- Anonim kimliği (tarayıcı verisi) silinmiş/yeni gelen ziyaretçi sepete eklerse: yeni boş sepetle başlar, hata görmez.
- Giriş yapan kullanıcının anonim sepeti boşsa: birleşme sessizce atlanır, hesap sepeti aynen kalır.
- Birleşme sırasında aynı ürün farklı fiyat/başlık taşıyorsa: kalem tekilliği ürün kimliğiyledir; güncel görünen değer kullanılır (sepet zaten fiyat gerçeğini checkout'ta doğrular — 056 modeli).
- Anonim ziyaretçi sepet sayfası açıkken başka sekmede giriş yaparsa: sonraki sepet görüntüleme hesabın (birleşmiş) sepetini gösterir.
- Chat asistanı üzerinden sepet işlemleri: yalnız girişli kullanıcı içindir, davranış değişmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem giriş yapmamış ziyaretçinin sepete ürün eklemesine, sepetini görmesine, adet değiştirmesine ve kalem silmesine izin VERMELİ; bu işlemler giriş ekranına yönlendirme YAPMAMALI.
- **FR-002**: Anonim sepet sunucuda kalıcı olmalı ve ziyaretçiye cihaz/tarayıcı-yerel kalıcı bir anonim kimlikle bağlanmalı; aynı cihazdan dönen ziyaretçi sepetini aynen bulmalı.
- **FR-003**: Bir ziyaretçinin anonim kimliği tahmin edilemez olmalı; bir ziyaretçi başka bir ziyaretçinin sepetine erişememeli.
- **FR-004**: Satın alma (checkout) girişi kimlik DOĞRULAMALI: anonim ziyaretçi "Satın Al" dediğinde giriş ekranına yönlendirilmeli, girişten sonra checkout'a geri dönmeli.
- **FR-005**: Giriş anında anonim sepet kullanıcının hesap sepetiyle birleşmeli: kalemler tek sepette toplanır, aynı ürünün adetleri toplanır, mevcut adet üst sınırı korunur (aşan kısım üst sınıra sabitlenir).
- **FR-006**: Birleşme sonrası anonim sepet ve anonim kimlik temizlenmeli; aynı anonim sepet ikinci kez birleşememeli.
- **FR-007**: Girişli kullanıcının mevcut sepet deneyimi değişmemeli (sepet hesapla anahtarlı kalır; chat/agent sepet akışları girişli-yol olarak aynen sürer).
- **FR-008**: Anonim sepete ekleme mevcut sepet kurallarına tabi kalmalı (adet üst sınırı vb.); anonim olmak kural gevşetmez.

### Key Entities

- **Sepet**: Mevcut kalıcı sepet; sahibi ya hesap kimliği ya da anonim kimliktir. İkisi aynı yapıdadır, fark yalnız sahip kimliğinin kaynağıdır.
- **Anonim kimlik**: Ziyaretçinin cihaz/tarayıcısına bağlı, tahmin edilemez, kalıcı tanıtıcı. Hesapla ilişkisi yoktur; birleşmede tüketilir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Giriş yapmamış ziyaretçi, siteye ilk girişten itibaren hiçbir giriş ekranı görmeden sepete ürün ekleyip sepetini yönetebilir.
- **SC-002**: Anonim sepetle "Satın Al" diyen ziyaretçi, giriş sonrası sepet kalemlerinin %100'ünü checkout'ta bulur (veri kaybı sıfır).
- **SC-003**: Hesap ve anonim sepetin birleşmesinde hiçbir kalem kaybolmaz; ortak ürünlerin adedi iki sepetin toplamıdır (üst sınır istisnası).
- **SC-004**: Girişli kullanıcıların mevcut sepet ve checkout akışı davranış değiştirmez (regresyon sıfır).

## Assumptions

- Anonim sepet süresiz yaşar (056 kararıyla uyumlu: sepette süre/rezervasyon yok). Terk edilmiş anonim sepetlerin temizliği bu feature'ın kapsamı DIŞI, ayrı feature.
- Anonim kimlik cihaz/tarayıcı-yerel tutulur; cihazlar arası anonim sepet taşıma kapsam DIŞI (hesap zaten bunu sağlar).
- Birleşme yönü tek: anonim → hesap. Çıkışta (logout) hesap sepeti anonime kopyalanmaz.
- Ürün detay/listeleme zaten anonim erişilebilir; bu feature yalnız sepet ve checkout kapısına dokunur.
- Sipariş, ödeme, adres akışları girişli kullanıcı gerektirmeye devam eder; bu feature oralara dokunmaz.
