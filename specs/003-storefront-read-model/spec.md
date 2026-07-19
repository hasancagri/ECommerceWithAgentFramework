# Feature Specification: Storefront Composite Read Model (Sipariş Detay Görünümü)

**Feature Branch**: `003-storefront-read-model`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "5 context'ten (Catalog, Stock, Order, Discount, Payment)
gelen veriyi birleştiren materialize edilmiş composite read model; ilk teslimat sipariş
detay görünümü; writer-publishes event-tetikli beslenme; kendi DB/servisi; ownership zorlar."

## Artefakt Ölçekleme Kademesi

**Tam (Full)**. Gerekçe: yeni bir bounded context (yeni servis + yeni DB + yeni şema),
servisler-arası yeni "changed" integration event'leri ve yeni sorgu kontratı getiriyor.
Anayasa: "yeni aggregate/tablo, servisler-arası event, yeni kontrat → tam akış."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sipariş detayı tek çağrıda birleşik döner (Priority: P1)

Bir müşteri kendi sipariş detayını açar; sipariş bilgisi, ürün adı/görseli, stok durumu,
uygulanan indirim ve ödeme durumu **tek bir okumada** birleşik olarak döner — istemci
5 ayrı servisi tek tek çağırmak zorunda kalmaz.

**Why this priority**: Feature'ın çekirdek değeri budur — dağıtık veriyi tek, hızlı,
birleşik bir görünümde sunmak. Tek başına teslim edilse bile kullanılabilir bir üründür.

**Independent Test**: Var olan bir sipariş için detay istenir; yanıtın 5 kaynağın da
alanlarını (sipariş, ürün, stok, indirim, ödeme) içerdiği tek çağrıda doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir sipariş ve ilgili ürün/stok/indirim/ödeme verisi mevcutken, **When**
   sipariş detayı istenir, **Then** tüm alanlar tek yanıtta birleşik döner.
2. **Given** bir kaynak alanı henüz raporlanmamışken (kısmi satır), **When** detay
   istenir, **Then** görünüm eksik alanlar için tutarlı bir "henüz bilinmiyor" değeri döner.
3. **Given** sipariş kalemleri satın-alma anındaki fiyatı taşırken, **When** ürün fiyatı
   sonradan değişir, **Then** kalemdeki snapshot fiyat değişmez (tarihsel gerçek korunur).

---

### User Story 2 - Kaynak veri değişince görünüm güncel yansıtır (Priority: P2)

Bir kaynak servis (Catalog/Stock/Order/Discount/Payment) sahibi olduğu veriyi
değiştirince, o değişiklik ilgili sipariş detay görünümüne kısa sürede yansır; görünüm
bayat kalmaz.

**Why this priority**: Materialize edilmiş görünümün değeri güncelliğine bağlıdır; ama
US1 çekirdek okumayı tek başına sağladığından, tazelik ikinci dilim olarak eklenir.

**Independent Test**: Bir sipariş görünümü okunur; ardından bir kaynak alanı (ör. ödeme
durumu, stok) değiştirilir; sonraki okuma yeni değeri yansıtır.

**Acceptance Scenarios**:

1. **Given** görünüm ödeme durumu "Pending" iken, **When** ödeme "Success" olur, **Then**
   sonraki okuma "Success" döner.
2. **Given** görünümde bir ürün "stokta" iken, **When** stok tükenir, **Then** sonraki
   okuma ürünü "tükendi" olarak yansıtır.
3. **Given** bir kaynak değişikliği iki kez yayınlanırsa (tekrar), **When** görünüm
   güncellenir, **Then** sonuç tek uygulamayla aynıdır (idempotent; çift sayım/mükerrer yok).

---

### User Story 3 - Kullanıcı yalnızca kendi siparişini görür (Priority: P2)

Sipariş detay verisi kullanıcıya özeldir; bir kullanıcı başka bir kullanıcının sipariş
detayını **göremez**. Yetki, görünümü sunan taraf tarafından zorlanır.

**Why this priority**: Doğruluk/güvenlik için gerekli; kişisel veri sızıntısı kabul
edilemez. Çekirdek okumaya (US1) sıkı bağlıdır ama ayrı test edilebilir bir kesittir.

**Independent Test**: A kullanıcısının token'ıyla B kullanıcısının sipariş detayı istenir;
erişim reddedilir. A kendi siparişini isterse başarılı döner.

**Acceptance Scenarios**:

1. **Given** A ve B kullanıcılarının ayrı siparişleri varken, **When** A, B'nin sipariş
   detayını ister, **Then** erişim reddedilir (veri sızmaz).
2. **Given** A kimliği doğrulanmışken, **When** A kendi sipariş detayını ister, **Then**
   görünüm döner.
3. **Given** kimlik doğrulanmamışken, **When** sipariş detayı istenir, **Then** istek reddedilir.

---

### Edge Cases

- **Kısmi satır**: Bir sipariş oluşup henüz stok/indirim/ödeme raporlanmadıysa görünüm
  eksik alanlarla tutarlı döner; hata fırlatmaz.
- **Sırasız event**: Kaynak event'leri farklı sırayla gelebilir; her alan kaynağına göre
  bağımsız güncellenir, geç gelen eski bir event güncel değeri ezmemelidir.
- **Mükerrer event**: Aynı değişiklik iki kez gelirse sonuç değişmez (idempotent upsert).
- **Bilinmeyen/eksik referans**: Görünümde bir ürün silinmişse (soft-delete) o durum
  kaynağın son bildirdiği haliyle yansır; görünüm otoriter karar üretmez.
- **Snapshot vs canlı**: Satın-alma anı alanları (kalem fiyatı) donuk; canlı alanlar
  (stok/ödeme durumu) kaynak event'iyle güncellenir. İkisi karıştırılmaz.
- **Bootstrap**: Görünüm ilk ayağa kalktığında geçmiş veri için başlangıç doldurması
  gerekir; event'ler yalnız bundan sonraki değişimi taşır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, bir sipariş için Order/Catalog/Stock/Discount/Payment kaynaklarından
  gelen alanları birleştiren tek bir sipariş-detay görünümü MUST sunsun.
- **FR-002**: Sipariş detayı MUST tek bir okuma isteğiyle dönsün; istemci ayrı servisleri
  tek tek çağırmasın.
- **FR-003**: Görünüm MUST materialize edilsin (önceden birleştirilip saklansın); okuma
  anında 5 kaynağa senkron çağrı yapılmasın.
- **FR-004**: Görünüm, her kaynağın yayınladığı "değişti" bildirimiyle MUST güncellensin;
  veriyi değiştiren servis (yazan) değişikliği yayınlar (writer-publishes).
- **FR-005**: Her alanın MUST tek bir otoriter sahibi olsun; görünüm otoriter veri
  üretmez, yalnızca kaynakların kopyalarını birleştirir.
- **FR-006**: Görünüm güncellemesi MUST idempotent olsun; aynı değişikliğin tekrarı sonucu
  değiştirmesin. Geç gelen eski bir bildirim güncel değeri ezmesin.
- **FR-007**: Satın-alma anı (snapshot) alanları MUST donuk kalsın; yalnızca canlı alanlar
  kaynak değişikliğiyle güncellensin.
- **FR-008**: Bir kullanıcı MUST yalnızca kendi siparişinin detayını görebilsin; başka
  kullanıcının detayına erişim reddedilsin. Yetki, görünümü sunan tarafça zorlanır.
- **FR-009**: Kimlik doğrulanmamış istekler MUST reddedilsin.
- **FR-010**: Bir kaynak alanı henüz raporlanmadıysa görünüm MUST kısmi (eksik alanlı) ama
  tutarlı dönsün; eksik veri hata sebebi olmasın.
- **FR-011**: Görünüm, kendi verisini kendi sınırında (kendi deposunda) MUST tutsun; başka
  servisin deposuna doğrudan erişmesin ve deposunu başka servisle paylaşmasın.
- **FR-012**: İlk teslimat kapsamı MUST yalnızca sipariş detay görünümü olsun; aynı desenle
  başka composite görünümler sonradan eklenebilir.
- **FR-013**: Görünümü sağlayan çözüm, ilk ayağa kalkışta mevcut siparişler için başlangıç
  doldurması (bootstrap) MUST sağlasın; yalnızca yeni değişikliklere bağlı kalmasın.

### Key Entities *(include if feature involves data)*

- **Sipariş Detay Görünümü (Order Detail Read Model)**: Bir siparişin 5 kaynaktan
  birleştirilmiş, denormalize, yassı kopyası. Nitelikler: sipariş kimliği/sahibi/durum/
  kalemler/toplam (Order), kalem başına ürün adı+görsel (Catalog), stok durumu (Stock),
  uygulanan indirim (Discount), ödeme durumu (Payment). Zengin aggregate değildir.
- **Alan Sahipliği (Field Ownership)**: Her görünüm alanının tek bir kaynak context'e ait
  olduğu eşleme. Görünüm bu eşlemenin dışında otoriter veri üretmez.
- **Değişiklik Bildirimi (Change Notification)**: Bir kaynak servisin sahibi olduğu veriyi
  değiştirdiğinde yayınladığı, görünümün güncellemesini tetikleyen olay. Kaynak başına ayrı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bir sipariş detayı, 5 kaynağın alanlarını içerecek şekilde **tek** okuma
  isteğiyle döner (istemci tarafında ek servis çağrısı = 0).
- **SC-002**: Bir kaynak veri değişikliğinin ardından ilgili görünüm en geç 5 saniye
  içinde güncel değeri yansıtır.
- **SC-003**: Aynı değişiklik bildirimi 100 kez tekrarlandığında görünüm sonucu, tek
  uygulamayla birebir aynıdır (idempotency doğrulanır).
- **SC-004**: Bir kullanıcının başka kullanıcının sipariş detayına erişme girişimi
  %100 reddedilir (kişisel veri sızıntısı = 0).
- **SC-005**: Bir veya birden çok kaynak alanı henüz raporlanmamışken bile detay isteği
  %100 tutarlı (hatasız, kısmi) yanıt döndürür.
- **SC-006**: Satın-alma anı fiyatı, ürün fiyatı sonradan değişse de görünümde değişmeden
  kalır (snapshot doğruluğu %100).

## Assumptions

- **Yaklaşım**: Görünüm, servisler-arası **integration event**'leriyle beslenen
  materialize edilmiş bir okuma modelidir; okuma anında senkron kaynak çağrısı yapılmaz.
- **Beslenme yönü**: Kaynak servisler görünümün varlığını bilmez; yalnızca kendi
  değişiklik olaylarını yayınlar. Görünüm downstream/conformist'tir, otorite değildir.
- **Bildirim içeriği (plan kararı)**: Olayın veriyi taşıyıp taşımayacağı (thin bildirim +
  geri-çekme, yoksa fat olay) ve dayanıklı yayın (outbox) plan aşamasında netleşir.
- **Tazelik modeli**: Anlık tazelik hedeflenir (event-tetikli); kısa süreli, kaynaklar
  arası eventual consistency kabul edilir. TTL/polling birincil mekanizma değildir.
- **Sınır**: Görünüm kendi deposuna sahiptir; paylaşılan veritabanı yoktur; cross-service
  iletişim yalnızca integration event ile olur (anayasa madde I).
- **Kapsam sınırı**: İlk sürüm yalnızca sipariş detay görünümünü kapsar; kullanıcıya-özel
  başka görünümler ve admin/toplu raporlama ertelenmiştir.
- **Kimlik**: Mevcut kimlik/yetki altyapısı (Identity.Server, scope tabanlı) yeniden
  kullanılır; ownership görünümü sunan tarafça zorlanır.
- **Bağımlılık**: Kaynak servislerin ilgili "değişti" olaylarını yayınlaması gerekir;
  bazıları yeni olarak eklenecektir. Mevcut mesajlaşma altyapısı yeniden kullanılır.
- **Caching ilişkisi**: Görünüm okumaları sonradan ayrı `002-aop-query-caching` feature'ı
  ile cache'lenebilir; bu feature caching mekanizmasını kapsamaz.