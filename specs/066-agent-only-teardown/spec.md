# Feature Specification: WebApp Müşteri Ekranları Söküm — Agent-Only Mağaza

**Feature Branch**: `066-agent-only-teardown`

**Created**: 2026-09-03

**Status**: Draft

**Input**: User description: "WebApp müşteri ekranlarını tümüyle sök — agent-only mağaza. Tüm müşteri
işlemleri zaten MCP'de (062-065 parite tamam). Kök (/) chat asistanına dönüşür; admin + login + chat +
BFF kalır."

**Kademe**: Tam — dış davranış değişir (müşteri yüzeyi tamamen kalkar, kök yeniden konumlanır),
çok dosyalı yıkım (WebApp UI + servis katmanı + Program.cs + Layout). Yeni aggregate/tablo/event YOK;
domain-TDD kapsam dışı (yalnız UI/BFF katmanı). Ön koşul: 062-065 MCP paritesi (BİTTİ).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ziyaretçi kökte chat asistanıyla alışveriş yapar (Priority: P1)

Bir ziyaretçi mağazanın adresini açar. Vitrin/ürün listesi yerine doğrudan **mağaza asistanı**
(chat) ile karşılaşır. Yazışarak kitap arar, sepete atar, sipariş verir, siparişini görüntüler,
adres ekler, yorum yazar, fiyat alarmı kurar — hiçbir klasik mağaza ekranı olmadan. Her işlem
arka planda agent'ın MCP tool'larıyla yürür.

**Why this priority**: Feature'ın varlık sebebi. "Agent-only mağaza" vizyonunun ana yüzeyi; bu akış
çalışmadan söküm anlamsız. Tek başına MVP.

**Independent Test**: WebApp'i aç → kök (`/`) chat asistanını göstermeli; sohbetten arama→sepet→
sipariş→görüntüle zinciri (MCP üzerinden) uçtan uca yürütülebilmeli.

**Acceptance Scenarios**:

1. **Given** temiz bir tarayıcı, **When** ziyaretçi mağaza köküne (`/`) gider, **Then** klasik vitrin
   değil chat asistanı arayüzü açılır (mağaza asistanı olarak konumlanmış).
2. **Given** kökteki chat, **When** kullanıcı "kitap ara / sepete at / sipariş ver / siparişimi göster"
   der, **Then** her adım agent üzerinden çalışır ve sonuç yazışmayla döner.
3. **Given** eski bir müşteri ekranı adresi (ör. `/Products/Index`, `/Basket`, `/Account/Profile`),
   **When** kullanıcı o adrese gider, **Then** kırık bir sayfa/500 değil temiz bir sonuç görür
   (404 ya da köke yönlendirme); menüde/sayfalarda o ekranlara giden kırık link kalmamıştır.

---

### User Story 2 - Admin yönetim yüzeyini kullanmaya devam eder (Priority: P2)

Bir yönetici giriş yapar ve ürün düzenleme + merchant onboarding ekranlarını önceki gibi kullanır.
Söküm yalnızca müşteri yüzeyini kaldırır; yönetim yüzeyi ve giriş akışı bozulmaz.

**Why this priority**: Yönetim mağazanın çalışması için şart; söküm admin'i etkilememeli. P1'in
üstünde ama ayrı bir güvence.

**Independent Test**: Admin kullanıcıyla giriş yap → ürün yönetimi (`/Admin/Products`) ve onboarding
(`/Admin/Onboarding`) ekranları açılır ve çalışır; login/OIDC akışı değişmemiştir.

**Acceptance Scenarios**:

1. **Given** admin rolündeki kullanıcı, **When** giriş yapar, **Then** yönetim paneline ulaşır ve
   ürün düzenleme + onboarding ekranları çalışır.
2. **Given** giriş akışı (SignIn/SignUp/OIDC), **When** söküm uygulanır, **Then** kimlik akışı
   davranış değişikliği olmadan çalışır.

---

### User Story 3 - Sistem sadeleşir, ölü yüzey ve bağımlılık kalmaz (Priority: P3)

Söküm sonrası WebApp yalnızca admin + login + chat + BFF barındırır. Müşteri ekranlarına ait
sayfalar, servis çağrı katmanı (müşteri REST istemcileri), yardımcı görsel parçalar ve gereksiz
yetki (scope) talepleri geride kalmaz; proje derlenir ve kırık referans içermez.

**Why this priority**: Bakım/temizlik güvencesi; ölü kod ve gereksiz yetki yüzeyi güvenlik ve
anlaşılırlık borcudur. P1/P2 çalışmadan anlamı yok.

**Independent Test**: Proje derlenir (0 hata); müşteri ekranı dosyaları + müşteri servis kayıtları +
müşteri yetki talepleri kaldırılmış; kalan yetki talepleri yalnız kimlik + yönetim yüzeyi içindir.

**Acceptance Scenarios**:

1. **Given** söküm tamamlanmış, **When** proje derlenir, **Then** kırık referans/derleme hatası yoktur.
2. **Given** söküm tamamlanmış, **When** WebApp'in talep ettiği yetkiler incelenir, **Then** yalnız
   kimlik yetkileri + yönetim yetkileri (katalog/stok yönetimi, merchant kimliği) kalmıştır; müşteri
   alışveriş yetkileri (sepet/sipariş/ödeme/müşteri-profili/yorum/alarm/vitrin) kalkmıştır.

---

### Edge Cases

- **Anonim ziyaretçi:** kökte chat anonim çalışır (giriş zorunlu değil); kimlik gerektiren işlemde
  agent kullanıcıyı girişe yönlendirir. Anonim gezinme yüzeyi (vitrin) artık yoktur — keşif chat'tedir.
- **Eski derin bağlantı (deep link):** kaldırılan bir müşteri sayfasına doğrudan gidiş temiz sonuç
  vermeli (404 veya köke yönlendirme); ham 500/istisna sayfası GÖRÜNMEMELİ.
- **Chat'in servis bağımlılığı:** chat yüzeyi agent'a (harici) bağlıdır, kaldırılan müşteri servis
  katmanına DEĞİL; söküm chat'i kırmamalı.
- **Admin footer/görsel:** müşteri görsel parçaları (ürün kartı, sayfalayıcı, sohbet açma ikonu vb.)
  kaldırılınca admin/login sayfalarında kırık gömü/eksik parça kalmamalı.
- **Görsel/keşif kaybı bilinci:** vitrin + SEO yüzeyi bilinçli olarak feda edilir (agent-only duruşu);
  bu bir hata değil kabul edilmiş kapsam kararıdır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, mağaza kökünü (`/`) klasik vitrin/ana sayfa yerine **mağaza asistanı (chat)**
  arayüzü olarak sunMALIdır; mevcut destek-sohbeti yüzeyi köke taşınıp "alışveriş asistanı" olarak
  yeniden konumlanır.
- **FR-002**: Sistem, tüm müşteri görsel ekranlarını (ana sayfa/vitrin, ürün listesi, ürün detay,
  kategori/yazar/yayınevi dizinleri, sepet, sipariş/checkout, hesap/profil) kaldırMALIdır.
- **FR-003**: Sistem, kaldırılan müşteri ekranlarına ait navigasyon/menü/görsel öğeleri (kategori
  şeridi, arama kutusu, "son gezdiklerim", ürün kartı/sayfalayıcı, sohbet-açma ikonu) Layout'tan
  temizleMELİ; kırık link veya eksik gömü bırakMAMALIdır.
- **FR-004**: Sistem, yalnızca müşteri ekranları tarafından kullanılan servis/istemci katmanını
  (vitrin, sepet, sipariş, ödeme, müşteri-profili, yorum, fiyat-alarmı istemcileri + anonim-sepet
  yardımcısı) ve bunların kayıtlarını kaldırMALIdır.
- **FR-005**: Sistem, WebApp'in müşteri alışveriş yetkilerini (vitrin/sepet/sipariş/ödeme/müşteri/
  yorum/alarm okuma-yazma) talep etmeyi bırakMALI; yalnız kimlik yetkileri + yönetim yetkileri
  (katalog yönetimi, stok yönetimi, merchant kimliği yönetimi) kalMALIdır.
- **FR-006**: Sistem, yönetim yüzeyini (ürün düzenleme + merchant onboarding) ve onun servis
  katmanını değişmeden koruMALIdır; admin giriş sonrası yönetim paneline ulaşMALIdır.
- **FR-007**: Sistem, kimlik/giriş akışını (giriş, kayıt, oturum) davranış değişikliği olmadan
  korumMALIdır.
- **FR-008**: Sistem, chat/asistan yüzeyini ve onun agent'a giden aktarım (proxy) yolunu korumMALI;
  chat, kaldırılan müşteri servis katmanına bağlı OLMAMALIdır (bağımsız proxy).
- **FR-009**: Kaldırılan bir müşteri ekranı adresine doğrudan erişim, ham hata (500/istisna) değil
  temiz bir sonuç (404 veya köke yönlendirme) verMELİdir.
- **FR-010**: Söküm sonrası proje derlenMELİ (kırık referans/derleme hatası yok) ve WebApp
  açılMALIdır: kök = chat, admin giriş → yönetim paneli, chat üzerinden uçtan uca alışveriş çalışır.

### Key Entities

Veri modeli değişmez (yeni aggregate/tablo/event yok). Etkilenen kavramsal yüzeyler:

- **Müşteri ekranı yüzeyi**: kaldırılacak görsel sayfalar + onların servis çağrı katmanı + görsel
  parçalar. Kaldırma hedefi.
- **Yönetim yüzeyi**: korunacak admin ekranları + servis katmanı. Değişmez.
- **Kimlik/oturum akışı**: korunacak giriş/kayıt/oturum. Değişmez.
- **Asistan (chat) yüzeyi**: köke taşınan, agent'a proxy'lenen ana yüzey. Korunur + yeniden konumlanır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ziyaretçi mağaza köküne gittiğinde klasik vitrin değil, **%100 chat asistanı** yüzeyiyle
  karşılaşır (vitrin/ürün-listesi ekranı hiç açılmaz).
- **SC-002**: Bağlantı sonrası alışveriş yaşam döngüsü (arama→sepet→sipariş→takip) **%100 yazışmayla**
  (agent üzerinden) tamamlanır; hiçbir klasik mağaza ekranı açılmaz.
- **SC-003**: Admin giriş → yönetim paneli akışı ve ürün düzenleme + onboarding ekranları söküm
  sonrası **aynen çalışır** (regresyon yok).
- **SC-004**: Kaldırılan müşteri ekranlarına giden **kırık link/eksik gömü sayısı = 0**; eski müşteri
  adreslerine doğrudan erişimde ham 500/istisna **görülmez**.
- **SC-005**: Söküm sonrası proje **0 derleme hatası** ile derlenir; WebApp'in talep ettiği yetkiler
  yalnız kimlik + yönetim yetkileridir (müşteri alışveriş yetkileri **kalmamıştır**).

## Assumptions

- **Ön koşul karşılandı:** Tüm müşteri işlemlerinin MCP tool paritesi 062–065'te tamamlandı
  (sepet/sipariş/ödeme-görüntüleme/adres/yorum/fiyat-alarmı/fiyat-geçmişi). Bu söküm o paritenin
  üzerine oturur; kaldırılan ekranların her işlevi agent üzerinden erişilebilir.
- **Chat bağımsız:** Asistan yüzeyi agent'a (harici orchestrator) proxy'lenir; WebApp'in müşteri
  servis katmanına bağlı değildir (kod-teyitli). Bu yüzden müşteri servisleri güvenle kaldırılabilir.
- **Kart ekleme ekransız değil (kapsam dışı):** Kart ekleme/silme MCP'de yoktur (mağazanın işi değil,
  PSP/ACP yolu). Kart yönetimi bu söküm kapsamında ele alınmaz; kullanıcı kartını başka yolla ekler,
  chat'ten yalnız seçer. Bu bilinçli bir kapsam sınırıdır.
- **Vitrin/SEO feda edilir:** Anonim vitrin + arama motoru görünürlüğü bilinçli olarak kaldırılır
  (agent-öncelikli duruş). Bu bir üretim standardı değil, vizyon/demo kararıdır.
- **Kalan yüzey:** WebApp söküm sonrası admin + login + chat + BFF barındırır; WebApp projesi
  tümüyle silinmez.
- **Müşteri ekranı adreslerinin akıbeti:** kaldırılan adreslerin köke yönlendirilmesi veya 404
  vermesi kabul edilebilir; plan aşamasında tek bir yol seçilir (varsayılan: köke yönlendirme,
  ziyaretçi asistana düşsün).
