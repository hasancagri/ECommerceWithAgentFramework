# Feature Specification: RBAC — Rol = Scope Demeti

**Feature Branch**: `030-rbac-scope-roles`

**Created**: 2026-08-06

**Status**: Draft

**Input**: User description: RBAC — rol tabanlı yetkilendirme; rol = token verme anındaki
scope demeti. Kapsam: Identity.Server (roller, rol→scope map, KnownScopes registry,
token'da rol→scope açılımı, seed, admin yönetim ekranları, register default rol).
Anayasa v1.7.0 İlke V.

**Artefakt Kademesi**: **Tam** — yeni tablolar (Roles, RoleScopes), yeni yetki mekanizması
ve admin yönetim yüzeyleri. Tam spec-kit akışı işletilir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kullanıcı rolüne göre yetkilenir (Priority: P1)

Login olan bir kullanıcı, TEK rolünün karşılık geldiği scope'ları taşıyan bir access token
alır. Servisler bu scope'lara göre izin verir; rolü hiç görmez. Uygun scope taşımayan
kullanıcı ilgili işlemi yapamaz.

**Why this priority**: Yetki modelinin belkemiği. Bu olmadan hiçbir rol bir şey ifade
etmez. MVP: kullanıcı doğru scope'ları alır, downstream scope zorlar.

**Independent Test**: Bootstrap admin ve bir customer ile login olup token'daki scope
claim'i incelenir; customer scope'u gerektiren uç customer'a açık, admin-scope'u
gerektiren uç customer'a kapalı, admin'e açıktır.

**Acceptance Scenarios**:

1. **Given** customer rolündeki kullanıcı, **When** login olur, **Then** token'ın scope
   claim'i customer rolünün map'lenmiş scope'larını (ör. catalog.read, basket.write,
   order.create/read, stock.reserve) içerir, admin scope'larını içermez.
2. **Given** admin rolündeki kullanıcı, **When** login olur, **Then** token admin rolünün
   scope'larını (ör. identity.roles.manage, catalog.write, feed.manage ...) içerir.
3. **Given** herhangi bir kullanıcı token'ı, **When** downstream servise gider, **Then**
   servis yalnız scope claim'ine bakar; token'da rol adı yetki için kullanılmaz.

---

### User Story 2 - Admin ekrandan rol, scope eşlemesi ve rol atamasını yönetir (Priority: P1)

`identity.roles.manage` scope'una sahip admin, bir yönetim ekranından: yeni rol yaratır,
role scope işaretler (yalnız bilinen scope listesinden), ve bir kullanıcının rolünü belirler
(kullanıcının rolü tektir; atama = mevcut rolü değiştirir). Serbest scope metni girilemez.

**Why this priority**: Feature'ın kullanıcı-değeri burada. İlk admin dışındaki tüm rol
yönetimi bu ekrandan yapılır; uyumsuzluk riski burada engellenir.

**Independent Test**: Admin ekranda yeni rol yaratır, scope'ları checkbox'tan işaretler,
bir kullanıcının rolünü bu role çevirir; o kullanıcı yeniden login olunca yeni scope'ları taşır.

**Acceptance Scenarios**:

1. **Given** admin scope'lu kullanıcı, **When** rol yönetim ekranını açar, **Then**
   seçilebilir scope listesi KnownScopes registry'sinden gelir (serbest metin girişi yok).
2. **Given** admin bir role scope işaretler, **When** kaydeder, **Then** rol→scope map DB'de
   güncellenir ve o rolü taşıyan kullanıcıların sonraki token'ları yeni scope kümesini alır.
3. **Given** admin bir kullanıcının rolünü değiştirir, **When** kullanıcı yeniden login olur
   / token yeniler, **Then** kullanıcı YENİ rolün scope'larını taşır, eski rolünkini değil.
4. **Given** `identity.roles.manage` scope'u OLMAYAN kullanıcı, **When** yönetim ekranına/
   uçlarına erişmeye çalışır, **Then** erişim reddedilir (403).
5. **Given** admin bir role scope atamaya çalışır, **When** listede olmayan bir string
   göndermeyi dener, **Then** sistem reddeder (yalnız KnownScopes kabul edilir).

---

### User Story 3 - Register olan kullanıcı otomatik customer olur ve direkt login olabilir (Priority: P2)

Yeni kullanıcı kayıt olduğunda sunucu ona TEK rol olarak `customer` atar; kullanıcı rol
seçemez. Aktivasyon-mail YOKtur; kayıt sonrası kullanıcı doğrudan login olabilir.

**Why this priority**: Yeni kullanıcıların sisteme temel erişim kazanması için gerekli,
ama US1/US2 backbone'u olmadan tek başına anlam taşımaz.

**Independent Test**: Yeni hesap açılır, hiçbir onay adımı olmadan login olunur ve
token'da customer scope'ları görülür.

**Acceptance Scenarios**:

1. **Given** yeni ziyaretçi, **When** kayıt olur, **Then** hesabına tek rol olarak
   `customer` atanır ve kayıt formunda rol seçimi sunulmaz.
2. **Given** yeni kayıtlı kullanıcı, **When** hemen login olur, **Then** ek onay/aktivasyon
   adımı olmadan giriş yapar ve customer scope'larını taşır.

---

### User Story 4 - Sistem açılışta rolleri, map'i ve bootstrap admin'i seed eder (Priority: P1)

Identity.Server açılışta (idempotent) `admin` ve `customer` rollerini, rol→scope map'ini
(KnownScopes'tan), login olabilen bir bootstrap admin kullanıcıyı ve makine client'larını
(ingestion-agent, order-saga) kurar. Var olanı bozmaz.

**Why this priority**: Tavuk-yumurta çözümü — ilk admin'i kimse atayamaz, seed atar.
US2 (admin ekranı) ancak bir admin var olduğunda kullanılabilir.

**Independent Test**: Temiz DB ile sistem başlatılır; bootstrap admin ile login olunur ve
admin scope'larıyla yönetim ekranına erişilir. Sistem yeniden başlatılınca duplike oluşmaz.

**Acceptance Scenarios**:

1. **Given** temiz veritabanı, **When** Identity.Server açılır, **Then** admin+customer
   rolleri, rol→scope map ve rolü admin olan bootstrap admin kullanıcı oluşur.
2. **Given** seed zaten koşmuş sistem, **When** yeniden açılır, **Then** mevcut kayıtlar
   duplike edilmez veya bozulmaz (idempotent).
3. **Given** açılış, **When** seed çalışır, **Then** bootstrap admin email+parolası
   yapılandırmadan (config/secret) okunur; kodda düz-metin parola sabiti bulunmaz.
4. **Given** açılış, **When** seed çalışır, **Then** makine client'ları (ingestion-agent,
   order-saga) client_credentials + statik scope ile kayıtlıdır.

---

### Edge Cases

- **Rol değişimi ile eldeki token**: Kullanıcının rolü değişse de eldeki access token eski
  scope'ları taşımaya devam eder; yeni yetki bir sonraki token'da gelir. Kısa access-token
  ömrü bu pencereyi sınırlar (anlık iptal/revocation kapsam dışı).
- **Kendini kilitleme (lockout)**: Sistemde en az bir admin rolünde kullanıcı kalmalı; son
  admin'in rolü başka bir role çevrilemez.
- **Seed rollerini silme**: `admin` ve `customer` rolleri silinemez (sistem onlara bağlıdır);
  scope map'leri düzenlenebilir ama roller yok edilemez.
- **Kullanıcısı olan rolü silme**: Kendisine kullanıcı atanmış bir rol silinmeye çalışılırsa
  engellenir (önce o kullanıcılar başka role taşınmalı).
- **Rolsüz kullanıcı olmaz**: Her kullanıcının tam olarak bir rolü vardır; rol atama daima
  mevcut rolü değiştirir (rol kaldırıp boş bırakma yoktur).
- **Var olan (mevcut) kullanıcılar**: Feature öncesi kayıtlı, rolü olmayan kullanıcılar bir
  kereye mahsus `customer` rolüyle geriye-doldurulur (backfill).
- **Bir servisin scope'u kaldırılırsa**: Kod bir scope'u KnownScopes'tan çıkarırsa, o scope'a
  yapılan rol→scope eşlemeleri artık geçersizdir; yönetim ekranı yalnız güncel registry'yi
  gösterir, kayıp eşleme sessizce yok sayılır (token'a yazılmaz).

## Requirements *(mandatory)*

### Functional Requirements

**Rol → scope mekanizması**

- **FR-001**: Her kullanıcının tam olarak BİR rolü olmalı; sistem bu rolü kalıcı saklamalı.
  Bir kullanıcı aynı anda birden çok rol taşıyamaz.
- **FR-002**: Sistem access token basarken, kullanıcının rolünü rol→scope map'inden
  scope'lara AÇMALI ve sonucu token'ın `scope` claim'ine yazmalı.
- **FR-003**: Downstream servisler yetki kararını YALNIZ scope ile vermeli; token'daki rol
  bilgisi (varsa) yetki için kullanılmamalı, rol-tabanlı policy uygulanmamalı.
- **FR-004**: Rol yalnız IdP'de yaşamalı; hiçbir downstream servis rol taksonomisini
  bilmemeli (BC izolasyonu).

**KnownScopes registry (kod-sahipli kapalı küme)**

- **FR-005**: Sistem, atanabilir tüm scope'ların KOD-sahipli, kapalı bir listesini
  (KnownScopes) sunmalı; bu liste yönetim yüzeyinin scope seçeneklerinin tek kaynağıdır.
- **FR-006**: Sistem, KnownScopes dışında bir scope string'inin bir role eşlenmesini
  REDDETMELİ (serbest metin yasak); eşleme yalnız listeden seçilir.
- **FR-007**: Her scope seçeneği kullanıcıya anlaşılır bir açıklama/etiketle sunulmalı.

**Rol yönetimi (admin ekranı)**

- **FR-008**: `identity.roles.manage` scope'una sahip admin yeni rol yaratabilmeli,
  var olan rolü düzenleyebilmeli (seed rolleri hariç silme).
- **FR-009**: Admin bir role scope'ları işaretleyerek (çoklu seçim) rol→scope map'ini
  düzenleyebilmeli; değişiklik kalıcı olmalı.
- **FR-010**: Admin bir kullanıcının rolünü belirleyebilmeli (mevcut rolü değiştirir);
  hedef kullanıcı ve rol var olan listelerden seçilmeli (serbest giriş yok).
- **FR-011**: Rol yönetimi yüzeyi ve uçları `identity.roles.manage` scope'u olmadan
  erişilemez olmalı (403).
- **FR-012**: Rol/scope/atama değişiklikleri, etkilenen kullanıcıların BİR SONRAKİ
  token'ında yansımalı (eldeki token değişmez).

**Register**

- **FR-013**: Yeni kullanıcı kaydı, hesaba tek rol olarak `customer` atamalı; kayıt akışında
  rol seçimi sunulmamalı.
- **FR-014**: Kayıt sonrası kullanıcı, aktivasyon/onay adımı OLMADAN doğrudan login
  olabilmeli (aktivasyon-mail bu feature kapsamı dışında).

**Seed (idempotent)**

- **FR-015**: Sistem açılışta `admin` ve `customer` rollerini ve bunların rol→scope
  map'ini (KnownScopes'tan) idempotent olarak oluşturmalı.
- **FR-016**: Sistem açılışta rolü admin olan, login olabilen bir bootstrap admin kullanıcı
  oluşturmalı; email ve parola yapılandırmadan (config/secret) alınmalı, kodda düz-metin
  parola olmamalı.
- **FR-017**: Seed idempotent olmalı: tekrar çalıştırıldığında duplike kayıt üretmemeli,
  mevcut kayıtları bozmamalı.
- **FR-018**: Sistem makine kimliklerini (ingestion-agent, order-saga) client_credentials +
  statik scope ile oluşturmalı/korumalı; bunlar rol/mail/kullanıcı kaydı taşımamalı (RBAC dışı).

**Bütünlük / güvenlik**

- **FR-019**: Sistem, sistemde admin rolünde en az bir kullanıcı kalmasını garanti etmeli:
  son admin'in rolü başka role çevrilemez.
- **FR-020**: `admin` ve `customer` seed rolleri silinemez; kendisine kullanıcı atanmış bir
  rol, o kullanıcılar başka role taşınmadan silinemez.
- **FR-021**: Feature öncesi rolü olmayan mevcut kullanıcılar bir kereye mahsus `customer`
  rolüyle geriye-doldurulmalı.

**Şeritler (yetki modeli sınırları)**

- **FR-022**: Anonim (kimliksiz) okuma yüzeyleri login olmadan erişilebilir kalmalı; RBAC
  bu yüzeyleri kapatmamalı.
- **FR-023**: İnsan login'i authorization_code akışıyla (parola login sayfası üzerinden)
  yapılmalı; parola grant (ROPC) kullanılmamalı.

### Key Entities *(include if feature involves data)*

- **Role (Rol)**: Adlandırılmış bir scope demeti. Nitelikler: benzersiz ad, silinebilir mi
  (seed rolleri korumalı). Scope'larla çok-çok; kullanıcılarla bir-çok (bir rol çok kullanıcı,
  bir kullanıcı tek rol).
- **RoleScope (Rol→Scope eşlemesi)**: Bir rolün bir KnownScope'a bağlanması. Yalnız
  KnownScopes'taki bir scope'a işaret edebilir.
- **User Role (kullanıcının rolü)**: Bir kullanıcının TEK rolü. Atama daima mevcut rolü
  değiştirir; kullanıcı rolsüz kalamaz.
- **KnownScope (bilinen scope)**: Kod-sahipli scope tanımı — kimlik (scope adı) + açıklama.
  Kaynağı kod; DB/ekran üretmez. Rol→scope eşlemesinin seçenek kümesidir.
- **Machine Client (makine kimliği)**: client_credentials ile kimliklenen insan-olmayan
  çağıran (ingestion-agent, order-saga); statik scope taşır, RBAC dışıdır.
- **Bootstrap Admin**: Seed'in yarattığı, rolü admin olan, login olabilen kullanıcı;
  kimlik bilgileri yapılandırmadan gelir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Temiz kurulumda, bootstrap admin ilk login'de admin scope'larıyla rol yönetim
  ekranına erişebilir (ek elle kurulum adımı olmadan).
- **SC-002**: Bir admin, yeni bir rol yaratıp scope işaretleyip bir kullanıcının rolünü ona
  çevirdikten sonra, o kullanıcının yeni token'ı %100 doğrulukla beklenen scope kümesini taşır.
- **SC-003**: Yönetim yüzeyinde geçersiz/uydurma scope string'i ile hiçbir rol→scope
  eşlemesi oluşturulamaz (0 uyumsuz eşleme).
- **SC-004**: `identity.roles.manage` scope'u olmayan hiçbir kullanıcı rol yönetim uçlarına
  erişemez (yetkisiz erişim oranı %0).
- **SC-005**: Yeni kayıt olan kullanıcı, kayıttan sonra ek onay adımı olmadan login olur ve
  customer işlemlerini (ör. sepete ekleme) yapabilir.
- **SC-006**: Sistem yeniden başlatıldığında seed duplike rol/kullanıcı/client üretmez
  (idempotent — kayıt sayıları sabit kalır).
- **SC-007**: Son admin'in rolünü değiştirme girişimi engellenir (sistem her zaman admin
  rolünde en az bir kullanıcı bulundurur).

## Assumptions

- **Kimlik altyapısı**: Identity.Server (OpenIddict + ASP.NET Identity, 029) mevcut ve
  yeniden kullanılır; roller ASP.NET Identity rol/kullanıcı-rol yapıları üzerine oturur
  (kullanıcı başına tek rol kısıtı uygulama katmanında zorlanır).
- **Access token ömrü**: Rol değişiminin makul sürede yansıması için kısa access-token ömrü
  (dakikalar mertebesi) + yenileme varsayılır; anlık token iptali (revocation) kapsam dışı.
- **Yönetim ekranı yeri**: Rol/scope/atama yönetim yüzeyi Identity.Server'ın kendi yönetim
  arayüzünde yaşar (rol otoritesi orada; downstream'e yayılmaz).
- **KnownScopes ilk içerik**: Registry, bugün servislerde tanımlı scope'larla (catalog.read/
  write, basket.write, order.*, stock.reserve, feed.manage, identity.roles.manage vb.) başlar.
- **Parola politikası**: ASP.NET Identity varsayılan parola/hesap politikaları kullanılır;
  ayrı bir politika tasarımı bu feature kapsamında değildir.
- **Aktivasyon-mail**: Bilinçli olarak kapsam dışı; ileride ayrı feature olarak eklenebilir.
- **Makine client'ları**: order-saga zaten mevcut (028); bu feature onu bozmadan seed'e
  dahil eder ve ingestion-agent client'ını ekler.
- **Downstream değişmez**: Servislerin scope-zorlama kodu (`.RequireAuthorization`,
  `ScopeAuthorizationMiddleware`) değişmez; feature yalnız token'a doğru scope'ların
  girmesini ve yönetimini sağlar.