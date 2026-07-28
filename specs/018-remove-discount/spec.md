# Feature Specification: Discount'ın Sistemden Tamamen Kaldırılması

**Feature Branch**: `018-remove-discount`

**Created**: 2026-07-28

**Status**: Draft

**Input**: User description: "Discount projesini ben kullanmıyorum, onunla alakalı her şeyi kaldırmak istiyorum. Discount mikroservisi (proje, DB, AppHost kaydı, gateway route'u), Storefront read model'deki indirim bileşimi, IngestionAgent workflow'undaki DiscountWrite adımı, Basket'teki kupon/indirim uygulaması, Shared'daki ilgili integration event'ler, Identity scope'ları, WebApp/ChatAgent'taki indirim izleri kaldırılsın; amaç sistemi sadeleştirmek."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sistem Discount servisi olmadan tam çalışır (Priority: P1)

Operatör sistemi ayağa kaldırdığında Discount servisi hiç başlamaz; kalan tüm akışlar
(vitrin, sepet, sipariş, ingestion) hatasız çalışır.

**Why this priority**: Servis kaldırma işleminin özü budur; kalan sistem bozulursa feature başarısızdır.

**Independent Test**: Sistem başlatılır; Discount resource'u listede yoktur ve uçtan uca alışveriş akışı tamamlanır.

**Acceptance Scenarios**:

1. **Given** sistem başlatıldı, **When** resource listesi incelenir, **Then** Discount servisi ve veritabanı yoktur.
2. **Given** vitrinde ürünler var, **When** kullanıcı ürünü sepete ekleyip sipariş verir, **Then** akış hatasız tamamlanır.
3. **Given** tüm çözüm derlenir ve testler koşulur, **When** build + test biter, **Then** hepsi geçer; Discount referansı kalmaz.

---

### User Story 2 - Alışveriş deneyiminde indirim izi kalmaz (Priority: P2)

Müşteri vitrinde ve sepette yalnız tek (liste) fiyatı görür; kupon girme alanı ve
indirimli fiyat gösterimi tamamen kaldırılmıştır.

**Why this priority**: Ölü UI kalıntısı (çalışmayan kupon alanı) kullanıcıyı yanıltır; sadeleştirme hedefinin görünür yüzüdür.

**Independent Test**: Vitrin ve sepet sayfaları açılır; hiçbir indirim/kupon öğesi görünmez, toplamlar tek fiyattan hesaplanır.

**Acceptance Scenarios**:

1. **Given** vitrin sayfası açık, **When** ürün kartları incelenir, **Then** yalnız tek fiyat görünür; indirimli fiyat/oran yoktur.
2. **Given** sepette ürün var, **When** sepet sayfası açılır, **Then** kupon alanı yoktur; toplam, birim fiyat × adet toplamıdır.
3. **Given** sipariş oluşturulur, **When** sipariş kaydı incelenir, **Then** siparişte indirim bilgisi yoktur.

---

### User Story 3 - Ingestion ve agent'lar indirim bilmez (Priority: P3)

Tedarikçi feed'i işlendiğinde ingestion zinciri indirim adımı olmadan tamamlanır;
sohbet agent'ı indirim araçları sunmaz.

**Why this priority**: Arka plan otomasyonunun sadeleşmesi; kullanıcıya görünmez ama işletim yükünü azaltır.

**Independent Test**: Feed tetiklenir; ürün Catalog+Stock'a yazılır, indirim adımı hiç koşmaz; ChatAgent araç listesinde indirim yoktur.

**Acceptance Scenarios**:

1. **Given** feed'de yeni ürün var, **When** ingestion çalışır, **Then** zincir indirim adımı olmadan başarıyla biter.
2. **Given** ChatAgent açık, **When** kullanılabilir araçlar listelenir, **Then** indirimle ilgili araç yoktur.
3. **Given** feed kaydında indirim alanı taşıyan eski biçim gelir, **When** kanonikleştirme yapılır, **Then** alan yok sayılır, akış bozulmaz.

---

### Edge Cases

- Eski yayınlanmış snapshot'larda indirim alanı vardı; alan kalkınca diff tüm ürünleri "değişmiş" saymamalı (gereksiz republish yok).
- Kuyruklarda bekleyen eski indirim event'i kalırsa tüketicisi yoktur; sistem çökmemeli, mesaj sessizce düşmelidir.
- Sepette daha önce kupon uygulanmış eski kayıt varsa okuma/yazma akışları hata vermemelidir (veri tabanları sıfırdan; risk düşük).
- Kimlik sunucusunda indirim scope'u isteyen eski token/istemci konfigürasyonu kalmamalıdır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Discount mikroservisi, testleri ve çözümdeki tüm proje kayıtları kaldırılmalıdır.
- **FR-002**: Orkestrasyon Discount servisini ve veritabanını tanımlamamalı; hiçbir servis ona referans vermemelidir.
- **FR-003**: Gateway, Discount'a yönlenen route taşımamalıdır.
- **FR-004**: Vitrin okuma modeli indirim bilgisi tutmamalı ve indirim event'i dinlememelidir.
- **FR-005**: Sepet; kupon uygulama/kaldırma yetenekleri, indirim değer nesnesi ve indirimli tutar hesapları olmadan çalışmalıdır.
- **FR-006**: Sipariş, indirim oranı taşımamalı; sipariş oluşturma girdisinde indirim alanı olmamalıdır.
- **FR-007**: Paylaşılan kontratlardan indirim event'i ve snapshot'taki indirim alanı kaldırılmalıdır; kuyruk/exchange sabitleri temizlenmelidir.
- **FR-008**: Tedarikçi feed maketi ve kanonikleştirme indirim alanları içermemelidir.
- **FR-009**: Ingestion zincirinden indirim yazma adımı kaldırılmalı; kalan adımlar aynı sırayla çalışmalıdır.
- **FR-010**: Kimlik sunucusu indirim scope/resource tanımlamamalı; ortak scope sabitleri ve istemci talepleri temizlenmelidir.
- **FR-011**: WebApp'te kupon UI'ı, indirim DTO/görünüm alanları ve Discount'a giden servis istemcisi kaldırılmalıdır.
- **FR-012**: ChatAgent, Discount MCP sunucusuna bağlanmamalı ve indirim aracı sunmamalıdır.
- **FR-013**: Kaldırmadan etkilenen tüm testler güncellenmeli; Discount test projesi silinmelidir.

### Key Entities

- **StorefrontView**: İndirim oranı alanı kalkar; yalnız katalog + stok bileşimi kalır.
- **Basket**: Uygulanan indirim ve satır bazında indirimli fiyat kavramları kalkar; toplam tek fiyattan hesaplanır.
- **Order**: İndirim oranı alanı kalkar.
- **SupplierProductSnapshot (kontrat)**: İndirim yüzdesi alanı kalkar; kalan alanlar değişmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kod tabanında (build çıktıları hariç) "discount" araması 0 anlamlı sonuç döner.
- **SC-002**: Sistem, önceki duruma göre bir servis ve bir veritabanı eksik olarak tam işlevle ayağa kalkar.
- **SC-003**: Uçtan uca alışveriş (vitrin → sepet → sipariş) ve ingestion akışı ilk denemede hatasız tamamlanır.
- **SC-004**: Tüm test paketi geçer; silinen Discount testleri dışında test sayısı azalmaz.

## Assumptions

- "Her şeyi kaldır" kupon akışını da kapsar: Discount servisi olmadan kupon çözülemez; sepetteki kupon özelliği tamamen silinir.
- Feed maketi bizim kontrolümüzdedir; indirim alanları maket veri setinden ve kontrattan da kaldırılır (yok sayma yerine silme).
- Veritabanları geliştirme ortamında sıfırlanabilir; eski indirimli sepet/sipariş verisi için göç (migration) yazılmaz.
- İleride indirim istenirse yeni bir feature olarak sıfırdan tasarlanır; geri alınabilirlik hedeflenmez (git geçmişi yeterli).
- Fiziksel `discountDb` volume kalıntısı geliştirici ortamında elle temizlenebilir; otomasyon kapsam dışıdır.