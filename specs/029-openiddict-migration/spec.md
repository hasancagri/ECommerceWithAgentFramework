# Feature Specification: OpenIddict Migrasyonu (Davranış Birebir)

**Feature Branch**: `029-openiddict-migration`

**Created**: 2026-08-06

**Status**: Draft

**Input**: User description: "Identity.Server'ı Duende IdentityServer'dan OpenIddict + ASP.NET Identity'ye taşı — davranış birebir korunarak."

> Artefakt kademesi: **Tam** — kimlik sağlayıcı değişimi tüm servislerin doğrulama zincirini keser; riskli teknik belirsizlikler var.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kullanıcı girişi ve alışverişi aynen sürer (Priority: P1)

Kayıtlı bir kullanıcı e-posta/şifresiyle girer; sepet, sipariş, profil gibi kullanıcıya bağlı tüm işlemler bugünkü gibi çalışır.
(Veritabanı geçişte sıfırlanır; kullanıcılar yeniden kayıt olur — geliştirme ortamı kabulü, veri taşıma yok.)

**Why this priority**: Kimlik katmanı tüm sistemin kapısı; giriş/alışveriş deneyimi bozulursa geçiş başarısızdır.

**Independent Test**: Kayıtlı bir kullanıcıyla login → sepete ekle → sipariş ver zinciri uçtan uca koşulur.

**Acceptance Scenarios**:

1. **Given** kayıtlı kullanıcı, **When** e-posta/şifresiyle giriş yapar, **Then** giriş başarılıdır; profil bilgileri görünür.
2. **Given** giriş yapmış kullanıcı, **When** sepete ürün ekler ve sipariş verir, **Then** akış bugünkü davranışla birebir tamamlanır.
3. **Given** giriş yapmış kullanıcı, **When** oturumu uzun süre açık kalır, **Then** oturum yenileme bugünkü gibi kullanıcıyı düşürmeden çalışır.

---

### User Story 2 - Yeni kullanıcı kaydı aynen çalışır (Priority: P2)

Yeni bir ziyaretçi, giriş ekranındaki kayıt yolculuğuyla (kayıt sayfasına yönlendirme dahil) hesap açar ve alışverişe başlar.

**Why this priority**: Kayıt kapısı kapanırsa yeni kullanıcı alınamaz; ama mevcut kullanıcı akışından sonra gelir.

**Independent Test**: Temiz tarayıcıyla kayıt yolculuğu koşulur; yeni hesapla giriş ve alışveriş doğrulanır.

**Acceptance Scenarios**:

1. **Given** hesabı olmayan ziyaretçi, **When** kayıt yolculuğunu tamamlar, **Then** hesap açılır ve otomatik/elle girişle alışverişe devam eder.
2. **Given** yeni kayıtlı kullanıcı, **When** hesabına bakılır, **Then** kullanıcıya herhangi bir rol atanmamıştır (rol işi sonraki feature).

---

### User Story 3 - Anonim gezinme girişsiz sürer (Priority: P2)

Giriş yapmamış bir ziyaretçi vitrini, ürün listelerini ve ürün detaylarını bugünkü gibi serbestçe gezer.

**Why this priority**: Vitrin ana trafik kapısıdır; anonim okuma kırılırsa site fiilen kapanır.

**Independent Test**: Oturumsuz tarayıcıyla ana sayfa + ürün listesi + ürün detayı gezilir.

**Acceptance Scenarios**:

1. **Given** giriş yapmamış ziyaretçi, **When** vitrin ve ürün sayfalarını gezer, **Then** hiçbir sayfa giriş istemez; içerik bugünkü gibi gelir.

---

### User Story 4 - Arka plan ve makine akışları kesintisiz çalışır (Priority: P1)

Kullanıcının görmediği makine akışları — sipariş tamamlama sagası, vitrin verisinin anonim okunması,
yapay zeka asistanının kullanıcı adına araç çağırması — geçişten etkilenmez.

**Why this priority**: Bu akışlar kırılırsa sipariş tamamlanamaz; hata kullanıcıya gecikmeli ve dolaylı yansır, teşhisi zordur.

**Independent Test**: Checkout tamamlanır (saga adımları dahil), asistan sohbetinde kullanıcıya özel araç çağrısı yapılır.

**Acceptance Scenarios**:

1. **Given** sepeti dolu kullanıcı, **When** checkout tamamlanır, **Then** sipariş onaylanır; stok düşer; sepet temizlenir (saga uçtan uca).
2. **Given** giriş yapmış kullanıcı, **When** asistandan kendi sepetiyle ilgili işlem ister, **Then** asistan kullanıcı kimliğiyle aracı çağırır.
3. **Given** çalışan sistem, **When** sepete ekleme stok rezervasyonunu tetikler, **Then** rezervasyon bugünkü gibi kullanıcı kimliğiyle yapılır.

---

### Edge Cases

- Geçiş anında açık oturumlar/yenileme biletleri: eski sağlayıcının verdiği oturumlar geçersiz olur; kullanıcı bir kez yeniden giriş yapar.
- Yanlış şifreyle giriş, kilitli/silinmiş hesap gibi hata yolları bugünkü davranışla aynı kalmalı.
- Kimlik sunucusu ayakta değilken servislerin davranışı (başlangıç sırası, doğrulama hatası) bugünkünden kötüleşmemeli.
- Yetkisiz istek (eksik yetkiyle yazma denemesi) bugünkü gibi reddedilmeli; ne fazla açık ne fazla kapalı.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kayıtlı kullanıcılar e-posta/şifreyle giriş yapabilmeli (veritabanı geçişte sıfırlanır; veri taşıma kapsam dışı).
- **FR-002**: Kayıt yolculuğu (girişten kayda yönlendirme dahil) bugünkü davranışla birebir çalışmalı.
- **FR-003**: Anonim ziyaretçi vitrin ve ürün sayfalarını girişsiz gezebilmeli.
- **FR-004**: Girişli kullanıcının yetki gerektiren tüm işlemleri (sepet, sipariş, profil...) bugünkü yetki kurallarıyla aynen çalışmalı.
- **FR-005**: Sipariş tamamlama sagasının makine kimliği akışı kesintisiz çalışmalı (stok düşümü + sepet temizliği dahil).
- **FR-006**: Vitrin verisinin anonim okunmasını sağlayan makine kimliği akışı aynen çalışmalı.
- **FR-007**: Asistan (agent) kullanıcı adına araç çağırırken kullanıcı kimliği bugünkü gibi taşınmalı.
- **FR-008**: Sepete ekleme sırasındaki stok rezervasyonu kullanıcı kimliğiyle bugünkü gibi çalışmalı.
- **FR-009**: Kimlik sağlayıcı dışındaki servislerin kodu ve yapılandırma sözleşmesi değişmemeli (sıfır değişiklik hedefi).
- **FR-010**: Kimlik sunucusunun adresi/imzalayan kimliği ve güvenli bağlantı zorunluluğu birebir korunmalı.
- **FR-011**: Bu feature'da kayıt olan kullanıcıya rol atanmamalı; rol modeli sonraki feature'ın konusudur.
- **FR-012**: Üç istemci kaydı (site, saga makinesi, anahtar yönetimi) ve on üç yetki kapsamı birebir taşınmalı.

### Key Entities

- **Kullanıcı hesabı**: E-posta, şifre (mevcut haliyle korunur), profil alanları; geçişte veri kaybı yok.
- **İstemci kaydı**: Sisteme token isteyen uygulamaların tanımı (site, saga makinesi, anahtar yönetimi); kod içinden tanımlanır.
- **Yetki kapsamı (scope)**: Servis bazlı okuma/yazma yetkilerinin tanımı; mevcut on üç kapsam aynen.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kayıt olan her kullanıcı e-posta/şifresiyle giriş yapabilir ve alışveriş akışlarını tamamlayabilir.
- **SC-002**: Canlı smoke listesi tek oturumda uçtan uca geçer: giriş, kayıt, anonim gezinme, sepet+sipariş, checkout saga, asistan aracı.
- **SC-003**: Kimlik projesi ve merkezi paket listesi dışında hiçbir serviste kod değişikliği yoktur (diff ile doğrulanır).
- **SC-004**: Yetkisiz erişim denemeleri geçiş öncesiyle aynı şekilde reddedilir (ne yeni açık, ne yeni engel).

## Assumptions

- Veritabanı geçişte sıfırlanır (Docker volume reset); veri taşıma ve eski oturum/bilet devri kapsam dışıdır. Kullanıcılar yeniden kayıt olur.
- İstemci/kapsam tanımları zaten kod içindedir; yeni sağlayıcıya kod seed'iyle taşınır.
- Kimlik sunucusunun dış adresi (issuer) değişmez; diğer servislerin yapılandırması bu adrese bağlı kalır.
- Rol/aktivasyon/yönetim ekranları bilinçli kapsam dışıdır (Feature 2); anayasa v1.6.0 zemini hazırlamıştır.