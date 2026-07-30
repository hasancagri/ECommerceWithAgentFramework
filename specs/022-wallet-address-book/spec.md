# Feature Specification: Wallet & AddressBook (Kayıtlı Kart + Adres Defteri)

**Feature Branch**: `022-wallet-address-book`

**Created**: 2026-07-30

**Status**: Draft

**Input**: User description: "Kullanıcının kayıtlı ödeme kartları (Wallet) ve kayıtlı
fatura/teslimat adresleri (AddressBook). İki ayrı aggregate root, UserId ile keyli;
SavedCard'da CVV/ham PAN ASLA saklanmaz (PCI). Ekle/sil/varsayılan-yap; en fazla 1
varsayılan. Doğal dil checkout için referans kaynağı; checkout anında entity'den VO
snapshot'a kopyalanır (snapshot dondurulur, kayıt sonradan değişse eski sipariş değişmez)."

**Artefakt kademesi**: **Tam** — iki yeni aggregate (Wallet, AddressBook), yeni
şema/tablolar, yeni endpoint kontratları, dış PaymentGateway contract'ına bağımlılık
(tokenize/charge) ve gelecekteki checkout'a referans besleme.

## Clarifications

### Session 2026-07-30

- Q: AddressBook hangi Bounded Context'te? → A: Yeni Customer BC açılır (Order BC'ye konmaz).
- Q: Wallet hangi BC'de? → A: Wallet da Customer BC'de (Payment değil); profil verisi tek serviste.
- Q: Gateway yokken kart-ekleme bu iterasyonda? → A: Simüle tokenize stub ile şimdi; US2 tam çıkar,
  gateway gelince stub swap (Wallet kodu değişmez).
- Q: Hangi işlemler agent'a (MCP) açılır? → A: Yalnız okuma (kart/adres listeleme); yazma REST/WebApp'te;
  kart-ekleme asla agent tool'u değil (ham PAN LLM'e girmez).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Adres defterini yönet (Priority: P1)

Kullanıcı fatura/teslimat adreslerini bir kez kaydeder; sonraki alışverişlerde tekrar
yazmadan seçer. Adres ekler, listeler, düzenler, siler ve birini varsayılan yapar.

**Why this priority**: Checkout'u hızlandıran en temel değer; kart olmadan tek başına
kullanışlı (adres defteri bağımsız yarar). Dış gateway bağımlılığı yok — MVP çekirdeği.

**Independent Test**: Wallet/gateway hiç olmadan; kullanıcı adres ekler, listeler, birini
varsayılan yapar, düzenler, siler — hepsi uçtan uca doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** giriş yapmış kullanıcı, **When** geçerli adres ekler, **Then** deftere eklenir
   ve listede görünür.
2. **Given** defterde birden çok adres, **When** birini varsayılan yapar, **Then** o adres
   varsayılan olur ve önceki varsayılan düşer (en fazla 1 varsayılan).
3. **Given** defterde bir adres, **When** onu düzenler, **Then** kayıt güncellenir; bu adresi
   referanslayan geçmiş sipariş snapshot'ları değişmez.
4. **Given** defterde bir adres, **When** onu siler, **Then** adres listeden kalkar.
5. **Given** boş/eksik alanlı adres, **When** ekleme dener, **Then** doğrulama hatası döner,
   kayıt oluşmaz.

---

### User Story 2 - Cüzdanı (kayıtlı kart) yönet (Priority: P1)

Kullanıcı kartını bir kez kaydeder; sonra "…1111 ile öde" diye referans verir. Kart ekler,
listeler (yalnız marka + son 4 hane + son-kullanma + etiket), siler, birini varsayılan yapar.

**Why this priority**: Hızlı ödemenin diğer yarısı ve doğal dil checkout'un referans kaynağı.
Adres defteriyle eşit önemde; güvenlik + dış gateway bağımlılığı daha ağır.

**Independent Test**: AddressBook'tan bağımsız; kullanıcı kart ekler, listede yalnız
marka+son4+son-kullanma+etiket görür (PAN/CVV asla), varsayılan yapar, siler.

**Acceptance Scenarios**:

1. **Given** giriş yapmış kullanıcı, **When** geçerli kart bilgisiyle kart ekler, **Then**
   gateway tokenize eder; cüzdana yalnız token + marka + son4 + son-kullanma + etiket yazılır.
2. **Given** kart ekleme, **When** kayıt tamamlanır, **Then** ham PAN ve CVV bu sistemde
   (DB, log, event) hiç saklanmaz; yalnız tokenize için gateway'e geçirilip atılır.
3. **Given** cüzdanda birden çok kart, **When** birini varsayılan yapar, **Then** yalnız o
   kart varsayılan olur (en fazla 1 varsayılan).
4. **Given** cüzdanda bir kart, **When** onu siler, **Then** kart listeden kalkar.
5. **Given** son-kullanma tarihi geçmiş kart, **When** ekleme dener, **Then** doğrulama
   hatası döner, kayıt oluşmaz.

---

### User Story 3 - Kayıtlı kayıtları checkout'ta referansla (Priority: P2)

Kullanıcı sipariş verirken kayıtlı bir kartı ve adresi seçer/referanslar; sistem o anki
değerleri siparişe **snapshot** kopyalar. Kayıt sonradan değişse/silinse eski sipariş değişmez.

**Why this priority**: Wallet/AddressBook'un varlık nedeni; ama checkout akışı (ödeme, taksit,
doğal dil, gateway charge) ayrı feature. Burada yalnız referanslama + snapshot kontratı.

**Independent Test**: Bir kayıtlı adres+kart referanslanıp siparişe kopyalanır; sonra kayıt
düzenlenir; eski siparişin adres/kart görünen bilgisinin değişmediği doğrulanır.

**Acceptance Scenarios**:

1. **Given** kayıtlı adres ve kart, **When** kullanıcı bunları sipariş için seçer, **Then**
   sipariş, o anki adres alanlarını ve kart görünen bilgisini (marka+son4) snapshot tutar.
2. **Given** siparişe kopyalanmış snapshot, **When** kullanıcı kaynağı düzenler/siler, **Then**
   geçmiş siparişin snapshot'ı değişmez.
3. **Given** "…1111 / ev adresim" gibi doğal dil referans, **When** referans tekile çözülür,
   **Then** doğru kayıt seçilir; belirsizse kullanıcıdan açık seçim istenir.

---

### Edge Cases

- Kullanıcının hiç adresi/kartı yokken listeleme → boş liste (hata değil).
- Tek kayıt varken ve o varsayılanken silinirse → varsayılan kalmaz; sonraki ekleme/seçim
  varsayılanı yeniden belirler (otomatik terfi yok — açık seçim).
- Başka kullanıcının kartını/adresini görme/silme denemesi → yetki reddi (yalnız kendi UserId).
- Kart eklerken gateway erişilemez/tokenize başarısız → kart **kaydedilmez**, kullanıcıya hata
  döner (yarım kayıt yok).
- Aynı kartın (aynı son4+son-kullanma) ikinci kez eklenmesi → sessiz kabul (mükerrer engellenmez);
  bkz. Assumptions.
- Geçmiş siparişçe referanslanan kartın/adresin silinmesi → sipariş snapshot tuttuğundan güvenle
  silinebilir; geçmiş sipariş etkilenmez.
- Kart silinirken gateway revoke erişilemez/başarısız → yerel silme yine tamamlanır (fail-open);
  revoke sonra yeniden denenir/loglanır, orphan token gateway sweep'iyle de temizlenebilir.

## Requirements *(mandatory)*

### Functional Requirements

**AddressBook**

- **FR-001**: Sistem, giriş yapmış kullanıcının adres defterine adres eklemesine izin vermeli
  (Province, District, Street, ZipCode, Line).
- **FR-002**: Sistem, adresin zorunlu alanlarını doğrulamalı; boş/eksik adres kaydı oluşturmamalı.
- **FR-003**: Kullanıcı kendi kayıtlı adreslerini listeleyebilmeli.
- **FR-004**: Kullanıcı kayıtlı bir adresi düzenleyebilmeli ve silebilmeli.
- **FR-005**: Kullanıcı bir adresi varsayılan yapabilmeli; deftede **en fazla 1** varsayılan
  adres bulunmalı (yeni varsayılan öncekini düşürür).

**Wallet**

- **FR-006**: Sistem, giriş yapmış kullanıcının cüzdanına kart eklemesine izin vermeli; kart,
  saklanmadan önce PaymentGateway'de tokenize edilmeli.
- **FR-007**: Sistem, kayıtlı kartta **yalnız** token + marka + son 4 hane + son-kullanma
  (ay/yıl) + etiket saklamalı.
- **FR-008**: Sistem, ham PAN ve CVV'yi **asla** kalıcı saklamamalı ve log/event'e yazmamalı;
  yalnız gateway tokenize çağrısına geçirip atmalı.
- **FR-009**: Sistem, son-kullanma tarihi geçmiş kartı reddetmeli.
- **FR-010**: Kullanıcı kendi kartlarını listeleyebilmeli; listede yalnız marka + son 4 hane +
  son-kullanma + etiket görünmeli (token dahil hassas alan görünmez).
- **FR-011**: Kullanıcı kayıtlı bir kartı silebilmeli.
- **FR-012**: Kullanıcı bir kartı varsayılan yapabilmeli; cüzdanda **en fazla 1** varsayılan
  kart bulunmalı.
- **FR-013**: Tokenize başarısızsa (gateway hatası/erişilemez) kart kaydedilmemeli; kullanıcıya
  hata dönmeli (fail-closed, yarım kayıt yok).
- **FR-013a**: Kart silindiğinde sistem, gateway'de o kartın token'ını **revoke** etmeli (orphan
  chargeable token bırakmamalı). Revoke **fail-open/best-effort**: önce yerel kayıt silinir, sonra
  revoke denenir; gateway erişilemezse silme yine başarılıdır (silme gateway'e bağlanmaz). Kart
  güncelleme = sil + yeniden ekle olduğundan güncelleme de eski token'ı revoke eder.

**Ortak / Checkout referansı**

- **FR-014**: Her kayıtlı adres ve kart bir kullanıcıya ait olmalı; UserId zorunlu değerdir
  (sahipsiz kayıt oluşamaz).
- **FR-015**: Bir kullanıcı yalnız kendi kayıtlarına erişebilmeli (görme/düzenleme/silme).
- **FR-016**: Kayıtlı adres ve kart, checkout tarafından **referanslanabilir** olmalı; checkout
  kayıttan siparişe değerleri kopyalar (snapshot).
- **FR-017**: Siparişe kopyalanan snapshot, kaynak kayıt sonradan değişse/silinse değişmemeli
  (geçmiş siparişler dondurulur).
- **FR-018**: Doğal dil bir referans ("…1111", "ev adresim", "iş kartım") tekile çözülemezse
  sistem kullanıcıdan açık seçim istemeli; sessizce yanlış kayıt seçmemeli.
- **FR-019**: Agent'a (MCP) yalnız **okuma** işlemleri açılmalı (kart/adres listeleme); ekle/sil/
  varsayılan-yap yalnız REST/WebApp'te olmalı. Kart ekleme **asla** bir agent tool'u olmamalı
  (ham PAN LLM turuna girmez).

### Key Entities *(include if feature involves data)*

- **Wallet** (aggregate root, **Customer BC**): Bir kullanıcının cüzdanı. `UserId` ile keyli;
  `SavedCard` koleksiyonunu ve "en fazla 1 varsayılan" invariant'ını korur.
- **SavedCard** (entity, Wallet içinde): Token, Brand, Last4, ExpiryMonth/Year, Label, IsDefault.
  Ham PAN/CVV **taşımaz**. Kimliği (Id) var, bağımsız yaşamaz.
- **AddressBook** (aggregate root, **Customer BC**): Bir kullanıcının adres defteri. `UserId` ile
  keyli; `SavedAddress` koleksiyonu + "en fazla 1 varsayılan" invariant'ı.
- **SavedAddress** (entity, AddressBook içinde): Province, District, Street, ZipCode, Line,
  IsDefault. Kimliği var, bağımsız yaşamaz.
- **Sipariş snapshot'ı** (VO, checkout'ta — bu feature'ın parçası değil, kontratı burada):
  Referanslanan adres/kartın kopyalanmış, dondurulmuş değeri (adres alanları + kart marka/son4).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı bir adresi 30 saniyeden kısa sürede, tek formda ekleyebilir.
- **SC-002**: Kart listeleme/detayı hiçbir koşulda ham PAN, CVV veya token göstermez (yalnız
  marka + son 4 hane + son-kullanma + etiket).
- **SC-003**: Defterde/cüzdanda her zaman en fazla 1 varsayılan kayıt bulunur (eşzamanlı
  varsayılan-yapma denemelerinde bile).
- **SC-004**: Kayıtlı bir adres/kart sonradan değiştirildiğinde, onu referanslayan geçmiş
  siparişlerin bilgisi %100 değişmeden kalır.
- **SC-005**: İkinci alışverişte kullanıcı, adres/kart yazmadan kayıtlıdan seçerek checkout'a
  hazır hale gelir.

## Assumptions

- **Gateway bağımlılığı**: Kart tokenize/charge dış PaymentGateway'in işidir (ayrı repo). Bu
  feature bir tokenize **contract'ı (soyut arayüz)** tanımlar ve bu iterasyonda arkasına **simüle
  stub** koyar (sahte token üretir); US2 tam çalışır. Gateway gelince yalnız stub gerçek çağrıyla
  değişir, Wallet kodu değişmez. Bkz. Obsidian `todo-payment-gateway-card-vault`.
- **Token = opak tutamak**: Wallet yalnız gateway'in döndürdüğü token'ı saklar; PAN/CVV bu
  sistemde hiç durmaz. Kullanıcı ayrımı token'da değil, Wallet'ın `UserId`'sindedir.
- **Card-on-file**: Kayıtlı kartla tekrar çekimde CVV istenmez (token yeter). PSP işlem-başı CVV
  isterse, CVV chat'ten değil LLM'siz güvenli alandan girilir; varsayılan card-on-file.
- **Kimlik**: `UserId`, Identity.Server token claim'idir; bu BC'lerde User aggregate yoktur —
  "sahipsiz olamaz" = `UserId` zorunlu-alan invariant'ıdır (containment değil).
- **Adres düzenlenebilir, kart düzenlenemez**: Adres update destekli; kart için update yok
  (sil + yeniden ekle; son-kullanma güncellemesi de sil+ekle). Her kart silme/değişim eski
  token'ı gateway'de revoke eder (fail-open; bkz. FR-013a). Bu iterasyonda revoke simüle stub'ta
  no-op; gateway gelince gerçek çağrıyla swap. Obsidian `todo-payment-gateway-card-vault`.
- **BC yerleşimi**: Wallet + AddressBook **yeni Customer BC**'de (yeni servis + DB `customerDb`
  + Aspire resource). Payment BC yalnız işlem/charge; profil verisi Customer'da toplanır.
- **Mükerrer kayıt**: Aynı kart/adresin ikinci kez eklenmesi engellenmez (sessiz kabul); ileride
  istenirse kural eklenir.
- **Kayıt üst sınırı**: Kullanıcı başına kart/adres sayısına sert üst sınır konmaz (makul kullanım).
- **Silme**: Kayıt silme geçmiş siparişleri etkilemez (siparişler snapshot tutar); soft-delete
  `AggregateRoot`'ta hazırdır.
- **Yetki**: Erişim scope-tabanlıdır (rol yok); okuma/yazma uygun scope'larla korunur.