# Feature Specification: Supplier Gateway + State'siz Ingestion

**Feature Branch**: `007-supplier-gateway-ingestion`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Tedarikçi veri akışını yeniden yapılandır: IngestionAgent'ı state'siz saf
yönlendiriciye indir, yeni Supplier.Gateway projesi ekle. Feed'i Gateway çeker, son yayınlanan snapshot'la
kıyaslar, yalnız yeni/değişen kaydı tek kanonik mesajla yayınlar; agent mesaj başına workflow ile MCP
yazımlarını yapar; staging/scheduler/run agent'tan silinir; hata retry + DLQ ile taşınır."

**Kademe**: Tam — yeni proje (Supplier.Gateway), yeni integration event kontratı ve servisler-arası akış
değişikliği var; anayasadaki "Küçük" koşullarının üçü birden bozuluyor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gateway yalnız yeni/değişen kaydı kanonik mesajla yayınlar (Priority: P1)

Sistem işletmecisi olarak, tedarikçi verisinin sisteme tek standart biçimde ve yalnız gerektiğinde
girmesini isterim. Değişmemiş kayıt iç sisteme hiç akmamalı; tedarikçiye özgü format sınırda kalmalı.

**Why this priority**: Kanonik kontrat ve değişiklik kapısı akışın temelidir; tüketici taraf buna yaslanır.

**Independent Test**: Gateway tek başına koşturulur; ilk çekimde tüm kayıtların, sonraki çekimde yalnız
değişenlerin broker'da mesaj ürettiği gözlemlenir.

**Acceptance Scenarios**:

1. **Given** Gateway DB'si boş ve feed'de N geçerli kayıt, **When** çekim koşar, **Then** N kanonik mesaj yayınlanır ve N snapshot kaydedilir.
2. **Given** hiçbir kayıt değişmemiş, **When** çekim koşar, **Then** hiçbir mesaj yayınlanmaz.
3. **Given** bir kaydın içeriği değişmiş, **When** çekim koşar, **Then** yalnız o kayıt için mesaj yayınlanır ve snapshot'ı güncellenir.
4. **Given** feed'e erişilemiyor veya feed boş, **When** çekim koşar, **Then** hata üretilmez, mesaj yayınlanmaz, sonraki periyot beklenir.
5. **Given** feed'de aynı harici kimlikten iki kayıt, **When** çekim koşar, **Then** ilki esas alınır, mükerrer olan elenir.

---

### User Story 2 - Agent mesajı state'siz işleyip domain'lere yönlendirir (Priority: P1)

Sistem işletmecisi olarak, gelen her tedarikçi kaydının katalog, stok ve indirime doğru sırayla
işlenmesini isterim. Agent bunun için kendi veritabanında hiçbir iz tutmamalı.

**Why this priority**: Akışın değer üreten yarısı; ürünler ancak bununla vitrine düşer.

**Independent Test**: Broker'a elle bir kanonik mesaj bırakılır; ürün, stok ve indirimin ilgili
servislerde güncellendiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** katalogda olmayan bir kayıt mesajı, **When** agent işler, **Then** ürün oluşur ve stok açılış miktarıyla açılır.
2. **Given** katalogda var olan bir kayıt mesajı, **When** agent işler, **Then** ürün güncellenir ve stok mesajdaki miktara eşitlenir.
3. **Given** indirim yüzdesi dolu bir mesaj, **When** agent işler, **Then** ürünün indirimi o yüzdeye ayarlanır.
4. **Given** indirim yüzdesi boş bir mesaj, **When** agent işler, **Then** üründe indirim varsa kaldırılır, yoksa işlem etkisiz başarıyla biter.
5. **Given** aynı mesajın broker tarafından yeniden teslimi, **When** agent tekrar işler, **Then** domain'lerde nihai durum değişmez.

---

### User Story 3 - Başarısız kayıtlar kaybolmaz, görünür kalır (Priority: P2)

Sistem işletmecisi olarak, bir kaydın işlenmesi başarısız olduğunda verinin kaybolmamasını ve
kalıcı hataların incelenebilir bir yerde birikmesini isterim.

**Why this priority**: Dayanıklılık olmadan akış üretimde güvenilmez; ama mutlu yol ondan önce gelir.

**Independent Test**: Bir domain servisi kapalıyken mesaj gönderilir; yeniden denendiği, servis dönünce
işlendiği ve ısrarlı hatanın dead-letter'a düştüğü gözlemlenir.

**Acceptance Scenarios**:

1. **Given** geçici hata (servis kapalı), **When** mesaj işlenemez, **Then** mesaj yeniden denenir ve servis dönünce başarıyla işlenir.
2. **Given** kalıcı hata (kayıt ısrarla reddediliyor), **When** denemeler tükenir, **Then** mesaj içeriğiyle birlikte dead-letter kuyruğuna düşer.
3. **Given** dead-letter'da bir mesaj, **When** işletmeci inceler, **Then** kaydın tamamı ve hata bağlamı mesaj üzerinden görülebilir.
4. **Given** Gateway yayın sonrası, snapshot kaydı öncesi çöker, **When** sonraki çekim koşar, **Then** kayıt yeniden yayınlanır ve tekrar işleme zararsızdır.

---

### User Story 4 - Eski staging/zamanlayıcı ağırlığı agent'tan silinir (Priority: P2)

Geliştirici olarak, IngestionAgent'ta yalnızca "mesaj al → workflow → MCP yazımı" kalmasını isterim;
staging veritabanı, hash kapısı, zamanlayıcı ve run sayaçları tamamen kalkmalı.

**Why this priority**: Feature'ın varlık sebebi bu sadeleşme; ama yeni akış çalışmadan silinemez.

**Independent Test**: IngestionAgent projesinde staging/run/scheduler tipleri ve veritabanı bağımlılığı
kalmadığı, uygulamanın yine de uçtan uca çalıştığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** yeni akış canlı, **When** eski parçalar silinir, **Then** agent kalıcı veri tutmaz ve uçtan uca akış çalışmaya devam eder.
2. **Given** silme tamamlandı, **When** çözüm derlenir ve testler koşar, **Then** hiçbir kırık referans ya da ölü kod kalmaz.

---

### Edge Cases

- Feed'den tamamen kaybolan ürün: kapsam dışı; snapshot Gateway'de, ürün domain'lerde son haliyle kalır.
- Aynı harici kimliğin tek feed içinde tekrarı: ilki esas, mükerrerler elenir.
- Yeniden teslim (at-least-once): domain yazımları aynı mesajın tekrar işlenmesinde aynı nihai duruma yakınsar.
- İndirimi olmayan ürüne "indirimi kaldır" çağrısı: hata değil, etkisiz başarı.
- Ürün oluştu ama stok/indirim yazımı patladı: mesaj yeniden teslim edilir; tekrar işleme güvenlidir.
- Gateway "yayınla" ile "snapshot kaydet" arasında çökerse: kayıt bir sonraki çekimde yeniden yayınlanır (kayıp yerine tekrar).
- DLQ'daki kayıt elle yeniden kuyruklanmazsa: domain, tedarikçi verisi yeniden değişene dek bayat kalır (bilinen sınır).
- Çekim sürerken yeni çekim tetiklenirse: üst üste binme engellenir.

## Requirements *(mandatory)*

### Functional Requirements

#### Supplier.Gateway (yeni bileşen)

- **FR-001**: Gateway tedarikçi feed'ini periyodik çeker ve zamanlamayı sahiplenir; periyot yapılandırılabilirdir.
- **FR-002**: Tedarikçiye özgü veri biçimi Gateway'in adapter'ında kanonik modele çevrilir; iç akış tedarikçi biçimi tanımaz.
- **FR-003**: Gateway kendi izole veritabanını kullanır; başka hiçbir bileşen bu veritabanına erişmez.
- **FR-004**: Gateway harici kimlik başına yalnız "en son yayınlanan snapshot"ı saklar; durum/işlenme bilgisi tutmaz.
- **FR-005**: Değişiklik kapısı: snapshot yoksa yayınla+kaydet; içerik aynıysa hiçbir şey yapma; farklıysa yayınla+üstüne yaz.
- **FR-006**: Sıralama: önce mesaj yayınlanır, sonra snapshot kaydedilir; çökme durumunda kayıp yerine tekrar tercih edilir.
- **FR-007**: Feed içi mükerrer harici kimliklerde ilk kayıt esas alınır; mükerrerler yayınlanmaz.
- **FR-008**: Erişilemeyen veya boş feed hata üretmez; o çekim mesajsız kapanır, sonraki periyotta yeniden denenir.
- **FR-009**: Çekimler üst üste binmez; önceki çekim sürerken yenisi başlatılmaz.

#### Kanonik mesaj kontratı

- **FR-010**: Tek kanonik mesaj tipi kullanılır; tedarikçi kimliği tip değil alandır (SupplierCode).
- **FR-011**: Mesaj, kaydın tedarikçideki güncel halini taşır: harici kimlik, ürün alanları, stok miktarı, opsiyonel indirim yüzdesi.
- **FR-012**: Kontrat bilinçli paylaşılan sözleşmeler arasında yaşar; Gateway ve agent yalnız bu kontrata bağımlıdır.

#### IngestionAgent (yeni hali)

- **FR-013**: Agent kanonik mesajın tüketicisidir; mesaj başına bir workflow koşar ve MCP araçlarıyla yazar.
- **FR-014**: Yazım sırası: önce katalog upsert; sonuç "oluştu" ise stok yazımı atlanır (açılış stoğu mevcut event akışıyla açılır).
- **FR-015**: Katalog sonucu "güncellendi" ise stok, mesajdaki miktara eşitlenir.
- **FR-016**: İndirim yüzdesi doluysa indirim o yüzdeye ayarlanır; boşsa indirim kaldırma çağrılır.
- **FR-017**: Agent hiçbir kalıcı veri tutmaz; staging kaydı, hash kapısı, run kaydı, zamanlayıcı ve feed çekimi agent'tan silinir.
- **FR-018**: Sonuç Gateway'e geri bildirilmez; akış tek yönlüdür ve "tamamlandı" işareti tutulmaz.

#### Dayanıklılık ve domain uyumu

- **FR-019**: Geçici işleme hataları otomatik yeniden denenir; denemeler tükenirse mesaj dead-letter kuyruğuna düşer.
- **FR-020**: Dead-letter'daki mesaj kaydın tamamını ve hata bağlamını korur; veri kaybı olmaz.
- **FR-021**: Domain yazımları aynı mesajın yeniden işlenmesine dayanıklıdır; nihai durum değişmez (at-least-once teslim).
- **FR-022**: İndirim kaldırma, indirimi olmayan üründe etkisiz başarıyla sonuçlanır (mevcut "bulunamadı" davranışı değişir).
- **FR-023**: Mevcut supplier-api (dış dünya maketi) değiştirilmez; Gateway ondan çeker.

### Key Entities

- **Kanonik tedarikçi snapshot mesajı**: Ürünün tedarikçideki güncel hali. SupplierCode, harici kimlik, ad/açıklama/marka/fiyat, stok miktarı, opsiyonel indirim yüzdesi.
- **Gateway snapshot kaydı**: Harici kimlik başına en son yayınlanan içerik. Durum alanı yoktur; tek işlevi sonraki çekimde değişiklik kıyası.
- **Supplier.Gateway**: Sınır bileşeni; tedarikçiyle konuşur, normalize eder, değişeni yayınlar. Kendi izole veritabanına sahiptir.
- **IngestionAgent (yeni hali)**: State'siz tüketici; mesaj başına workflow ile katalog → stok → indirim yönlendirmesi yapar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tedarikçi feed'ine eklenen yeni ürün, en geç bir çekim periyodu + 1 dakika içinde vitrinde görünür.
- **SC-002**: Feed'de değişen stok/fiyat/indirim, aynı süre içinde ilgili servislere yansır.
- **SC-003**: İçeriği değişmemiş kayıt için hiçbir mesaj ve hiçbir domain yazım çağrısı üretilmez.
- **SC-004**: IngestionAgent kayıt başına hiçbir kalıcı satır üretmez (veritabanı bağımlılığı sıfır).
- **SC-005**: Bir domain servisi 5 dakika kapalı kalsa bile o aralıkta gelen kayıtların %100'ü servis dönünce işlenir.
- **SC-006**: Kalıcı başarısız kayıtların %100'ü, içeriğiyle birlikte dead-letter üzerinden incelenebilir.
- **SC-007**: Aynı mesajın iki kez işlenmesi domain'lerde nihai durumu değiştirmez.

## Assumptions

- Tek tedarikçi vardır; kaygıya göre mesaj bölme (ürün/stok ayrı) ikinci tedarikçi gelirse değerlendirilir.
- Çekim periyodu bugünkü davranışla aynı başlar (30 dk) ve Gateway'de yapılandırılabilir.
- Tedarikçi kaynaklı ürünler e-ticaret tarafından değiştirilmez; elle yapılan değişiklik ancak tedarikçi verisi değişince düzelir.
- Mesajlaşma altyapısı at-least-once teslim, otomatik retry ve dead-letter yeteneği sağlar.
- Katalog upsert ucu senkron "oluştu/güncellendi" sonucu döndürmeye devam eder.
- Operatör görünürlüğü (kayıt başına Completed/Failed telemetrisi) bilinçli ertelenmiştir; DLQ görünürlüğü ilk sürüm için yeterlidir.
- DLQ'dan yeniden kuyruklama ilk sürümde manuel bir işletim adımıdır.
- Feed'den kaybolan ürünlerin pasifleştirilmesi kapsam dışıdır.