# Feature Specification: External MCP UserKey

**Feature Branch**: `004-external-mcp-userkey`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "MCP sunucularını dışa açmak için per-user opak UserKey ile custom authentication"

**Artefakt kademesi**: **Tam** — yeni kalıcı tablo (`ApiKeys`), yeni key→kullanıcı
çözümleme kontratı ve yeni bir authentication şeması (servisler-arası kesişen) gerekir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tek kalıcı anahtarla gerçek kullanıcı adına yazma (Priority: P1)

Dış tüketici (n8n, bir arkadaş, üçüncü taraf) elindeki tek opak `UserKey` dışında
hiçbir şeyle uğraşmadan, sistemde **belirli bir gerçek kullanıcının adına** yazma
işlemi yapar (ör. o kullanıcının sepetine ürün ekler).

**Why this priority**: Feature'ın var oluş nedeni bu — dışa açılan MCP'nin asıl değeri
"kimlik zahmeti olmadan kullanıcı adına işlem". Bu olmadan feature'ın anlamı yok.

**Independent Test**: Bir kullanıcı için anahtar verilir, o anahtarla bir yazma isteği
gönderilir; işlemin o kullanıcının verisi üzerinde gerçekleştiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir kullanıcıya bağlı geçerli anahtar, **When** o anahtarla yazma isteği
   gelir, **Then** işlem o kullanıcının kimliğiyle (claim'leriyle) yapılır ve başarılıdır.
2. **Given** hiç anahtar taşımayan bir yazma isteği, **When** istek gelir, **Then**
   reddedilir (yetkisiz).
3. **Given** geçersiz/bilinmeyen bir anahtar, **When** yazma isteği gelir, **Then**
   reddedilir — anonim olarak sessizce geçmez.

---

### User Story 2 - Anahtar yaşam döngüsü: verme ve anında iptal (Priority: P2)

Yetkili operatör (sistem sahibi) bir kullanıcı için anahtar üretir ve istediği anda o
anahtarı iptal edebilir; iptal edilen anahtar bir daha çalışmaz.

**Why this priority**: Güvenlik ve kontrol. Anahtar kalıcı olduğu için, tek geri-alma
mekanizması iptaldir; iptalin gerçekten ve hızla devreye girmesi şarttır.

**Independent Test**: Bir anahtar verilir ve çalıştığı görülür; iptal edilir; aynı
anahtarla sonraki istek reddedilir.

**Acceptance Scenarios**:

1. **Given** yetkili operatör, **When** bir kullanıcı için anahtar ister, **Then** o
   kullanıcıya bağlı, süresiz geçerli bir anahtar üretilir.
2. **Given** çalışan bir anahtar, **When** operatör onu iptal eder, **Then** sonraki
   istekler o anahtarla reddedilir.
3. **Given** aynı kullanıcının iki farklı anahtarı, **When** biri iptal edilir, **Then**
   diğeri çalışmaya devam eder.

---

### User Story 3 - Anahtarsız anonim okuma (Priority: P3)

Dış tüketici, hiçbir anahtar taşımadan (anonim) dışa açık okuma işlemlerini yapabilir.

**Why this priority**: Okumalar bilinçli olarak public'tir; giriş engelini düşürür.
Yazma değeri (P1) sağlandıktan sonra gelir.

**Independent Test**: Hiç anahtar göndermeden bir okuma isteği yapılır; başarıyla veri
döndüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** anahtar taşımayan bir istek, **When** dışa açık bir okuma işlemi çağrılır,
   **Then** istek anonim olarak başarılıdır.
2. **Given** anahtar taşımayan bir istek, **When** bir yazma işlemi çağrılır, **Then**
   reddedilir (okuma/yazma ayrımı işlem düzeyinde uygulanır).

---

### User Story 4 - Kayıtta yetki (scope) seçimi (Priority: P2)

Kullanıcı kayıt olurken ekranda operatörün belirlediği yetki (scope) listesini görür ve
istediklerini seçer. Sonrasında anahtarı yalnızca seçtiği yetkiler kadar iş yapabilir.

**Why this priority**: Anahtarın "ne yapabileceğini" belirleyen kaynak budur; en az
ayrıcalık burada, kullanıcı onayıyla kurulur. Yazma değeri (P1) bu seçime dayanır.

**Independent Test**: Kayıtta yalnızca "okuma" seçen kullanıcının anahtarıyla yazma
denenir ve reddedilir; "yazma" seçenin anahtarıyla aynı işlem başarılı olur.

**Acceptance Scenarios**:

1. **Given** kayıt ekranı, **When** kullanıcı bir scope alt kümesi seçer, **Then** bu
   seçim kullanıcıya bağlı olarak kalıcılaşır (UserScopes).
2. **Given** yalnızca okuma yetkisi seçmiş kullanıcı, **When** anahtarıyla yazma çağırır,
   **Then** yetkisiz reddedilir.
3. **Given** operatörün listede sunmadığı bir yetki, **When** istek gelir, **Then**
   kullanıcı onu seçemez/edinmez (yalnızca operatör-tanımlı scope'lar sunulur).

---

### Edge Cases

- Yazmada anahtar yok → reddedilir; okumada anahtar yok → izin verilir.
- Geçersiz/bilinmeyen anahtar → hem okuma hem yazmada reddedilir (anonime düşmez).
- İptal edilmiş anahtarla istek → reddedilir.
- Anahtarın bağlı olduğu kullanıcı silinmiş/pasif → anahtar reddedilir.
- Aynı anahtarın birden çok tüketiciden eşzamanlı kullanımı → engellenmez (kullanıcı adına).
- Anahtarın string'i kurcalanır (bir karakter değişir) → başkasının kimliğine bürünülemez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Yetkili operatör, admin-only bir uç nokta üzerinden mevcut belirli bir
  kullanıcıya bağlı bir UserKey üretebilmeli.
- **FR-002**: Bir UserKey, anonim/makine kimliği değil, **gerçek bir kullanıcının**
  kimliğine (claim'lerine) karşılık gelmeli.
- **FR-003**: Bir UserKey'in **son kullanma tarihi olmamalı** — iptal edilene dek süresiz geçerli.
- **FR-004**: Yetkili operatör, admin-only bir uç nokta üzerinden bir UserKey'i istediği
  anda iptal edebilmeli.
- **FR-005**: İptal edilen bir UserKey, sonraki tüm isteklerde reddedilmeli; iptal
  neredeyse-anında (≤ birkaç saniye) etkili olmalı.
- **FR-006**: **Tüm** mevcut okuma işlemleri (tool/query) dışa anonim (anahtarsız)
  açılmalı; bu read'lerden scope şartı kalkar (iç ChatAgent çağrıları dahil).
- **FR-007**: Dışa açık **yazma** işlemleri geçerli bir UserKey istemeli; anahtarsız
  veya geçersiz/iptalli anahtarlı yazma istekleri reddedilmeli.
- **FR-008**: Geçerli anahtar taşıyan istek, temsil ettiği kullanıcının **scope'larıyla**
  (UserScopes) yetkilendirilmeli; rol getirilmez, yalnızca scope kullanılır (anayasa V).
- **FR-013**: Kullanıcı kayıt sırasında, operatör-tanımlı bir listeden istediği scope'ları
  seçebilmeli; seçim kullanıcıya bağlı kalıcılaşır. Listede olmayan scope edinilemez.
- **FR-014**: Bir kullanıcının tüm anahtarları aynı (kullanıcının) scope setini paylaşır;
  scope anahtara değil kullanıcıya bağlıdır.
- **FR-009**: Geçersiz/bilinmeyen anahtar reddedilmeli (yazmada anonime düşürülmeden).
- **FR-010**: Anahtarın ham değeri, sızıntı halinde kullanılabilir anahtar vermeyecek
  biçimde saklanmalı (ham/geri-döndürülebilir saklanmamalı).
- **FR-011**: Dış anahtara varsayılan geniş yazma yetkisi verilmemeli; yetki, kullanıcının
  kayıtta seçtiği scope'larla sınırlı kalmalı (en az ayrıcalık; default boş = salt-okuma).
- **FR-012**: Dışa açık **MCP yüzeyinde**, bir işlemin anahtar gerektirip gerektirmediği
  çağıranın ağ yolu/gateway'i ile değil **işlemin kendisiyle** (okuma/yazma) belirlenmeli.
  (REST API iç WebApp yoludur; mevcut gateway policy'leri bu feature kapsamı dışıdır.)

### Key Entities *(include if feature involves data)*

- **UserKey (API Key)**: Bir kullanıcıya bağlı opak sır. Nitelikler: tanımlayıcı,
  sahip kullanıcı, durum (aktif/iptalli), oluşturma zamanı. Scope taşımaz, exp yok.
- **UserScope**: Bir kullanıcıya bağlı scope kaydı (kayıtta seçilen). Kullanıcının
  yetkisini belirler; anahtar bu scope'ları miras alır.
- **User**: Anahtarın temsil ettiği mevcut kimlik (claim'ler: sub/email/ad/soyad).
- **Dış Tüketici**: Anahtarı taşıyan taraf (n8n, arkadaş, üçüncü taraf) — kaydı tutulmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Dış tüketici, tek bir anahtar değeri dışında hiçbir giriş/token-değişim
  adımı yapmadan bir kullanıcı adına yazma işlemini tamamlayabilir.
- **SC-002**: İptal edilen anahtar, iptalden sonra ≤ 5 saniye içinde çalışmaz olur.
- **SC-003**: Bugün verilen bir anahtar, uzun bir süre (ör. 1 yıl) sonra hiçbir
  yenileme olmadan çalışmaya devam eder.
- **SC-004**: Okuma işlemleri sıfır kimlik bilgisiyle başarılı olur.
- **SC-005**: Kurcalanmış/uydurma anahtar değeriyle yazma denemesi %100 başarısız olur
  (anahtarı değiştirerek başka kullanıcı olunamaz).
- **SC-006**: Yeni bir dış tüketiciyi devreye almak tam olarak tek bir değerin
  (anahtar) paylaşılmasını gerektirir, başka hiçbir şey değil.

## Assumptions

- **Kademe = Tam**: Yeni `ApiKeys` tablosu, yeni çözümleme kontratı ve yeni bir
  authentication şeması getirir; küçük-kademe eşiğini aşar.
- Anahtar→kullanıcı sahipliği **Identity.Server**'da kalır (mevcut `IdentityDbContext`).
- Yalnızca **yazmalar** anahtar gerektirir; okumalar tasarımca public'tir.
- Mevcut **scope-tabanlı** yetkilendirme yeniden kullanılır; rol getirilmez (anayasa V).
- Kullanıcının yetkisi **kayıtta seçilir** ve `UserScopes` ile kullanıcıya bağlanır;
  anahtar scope taşımaz, kullanıcının scope'larını miras alır (RBAC/rol değil).
- Kayıt ekranında sunulan scope kümesini **operatör** tanımlar; kullanıcı alt küme seçer.
- **Anayasa notu**: Authentication'a ikinci bir şema (JWT-olmayan) eklenir; ancak
  ilke V'in özü — rol değil **scope** tabanlı yetki — korunur; servisler değişmez.
- İptalin anındalığı, anahtarın yalnızca seyrek yazmalarda çözülmesiyle sağlanır
  (agresif cache yok); okumalar anahtar-yolu dışıdır.
- **Anahtar yönetimi (karar)**: Verme + iptal, Identity.Server'da admin-only iki uç
  nokta ile yapılır (manuel/seed değil).
- **Dışa açık okuma (karar)**: Tüm mevcut okuma işlemleri anonim açılır; read'lerden
  scope şartı kalkar — iç ChatAgent okumaları da artık anonim geçer (kabul edilen etki).