# Feature Specification: Fiyat Alarmı + Mail Bildirimi

**Feature Branch**: `060-price-alarm-mail`

**Created**: 2026-09-02

**Status**: Draft

**Input**: User description: "Fiyat alarmı + mail bildirimi. Kullanıcı ürün detayında 'Fiyat Alarmı Ekle' der (login kapılı); alarm yeni 'kitaplık' BC'sinde saklanır. Catalog fiyat değişince kitaplık BC dinler; fiyat düşmüşse ve eşleşen alarm varsa tetik event yayınlar. Yeni DB'siz bildirim agent worker'ı (MAF Workflows) maili kişisel içerikle üretir ve Mail.Mcp üzerinden gönderir; mailler Mailpit'te görülür. Alarm v1 tek atımlık."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Alarm kurma ve kaldırma (Priority: P1)

Ayşe bir kitabı beğenir ama fiyatını yüksek bulur. Detay sayfasında "Fiyat Alarmı Ekle" düğmesine basar; giriş yapmamışsa önce giriş sayfasına yönlenir (sepetteki desenle aynı). Alarm kurulunca düğme "Alarm Kurulu"ya döner; vazgeçerse aynı yerden alarmı kaldırır.

**Why this priority**: Alarm kaydı olmadan tetik de mail de yok — zincirin ilk halkası. Tek başına bile değer taşır ("ilgileniyorum" kaydı).

**Independent Test**: Login'li kullanıcı alarmı kurar → sayfa yenilenince "Alarm Kurulu" görünür; kaldırınca düğme ilk hâline döner. Anonim kullanıcı düğmeye basınca login'e gider.

**Acceptance Scenarios**:

1. **Given** giriş yapmış kullanıcı, **When** detayda "Fiyat Alarmı Ekle"ye basar, **Then** alarm kaydedilir ve düğme "Alarm Kurulu" durumuna geçer.
2. **Given** anonim ziyaretçi, **When** düğmeye basar, **Then** giriş sayfasına yönlenir; girişten sonra detaya döner.
3. **Given** alarmı kurulu kullanıcı, **When** aynı ürüne tekrar alarm kurmayı dener, **Then** ikinci kayıt oluşmaz (aynı ürüne tek alarm).
4. **Given** alarmı kurulu kullanıcı, **When** "Alarmı Kaldır" der, **Then** alarm silinir ve fiyat değişse bile mail gelmez.

---

### User Story 2 - Fiyat değişince mail gelir (Priority: P1)

Yönetici kitabın fiyatını değiştirir. Kısa süre içinde Ayşe'ye kişisel bir e-posta gelir: kitabın adı, eski ve yeni fiyat, kitaba giden bağlantı. Alarm açık kalır (abonelik gibi): sonraki her fiyat değişiminde yine mail gelir; rahatsız olan kullanıcı alarmı kendisi kaldırır.

**Why this priority**: Feature'ın var oluş nedeni; US1 ile birlikte uçtan uca değer bu ikisinde tamamlanır.

**Independent Test**: Alarm kur → admin'den fiyatı değiştir → mail kutusunda (test posta arayüzü) kişisel mail görünür; alarm hâlâ kurulu. Fiyatı bir daha değiştir → ikinci mail gelir.

**Acceptance Scenarios**:

1. **Given** ürüne alarmı olan kullanıcı, **When** ürünün fiyatı değişir (artış ya da düşüş), **Then** kullanıcının kayıt e-postasına ürün adı + eski/yeni fiyat + ürün bağlantısı içeren mail gider.
2. **Given** aynı ürüne alarmı olan birden çok kullanıcı, **When** fiyat değişir, **Then** her birine kendi maili gider.
3. **Given** mail gönderilmiş alarm, **When** fiyat TEKRAR değişir, **Then** yeni bir mail daha gider (alarm kaldırılana dek yaşar).
4. **Given** alarmı kaldırılmış kullanıcı, **When** fiyat değişir, **Then** mail gitmez.

---

### User Story 3 - Bildirim izi (Priority: P3)

Sistem her gönderilen maili bir "bildirim gönderildi" kaydı olarak duyurur. v1'de kullanıcıya ekran yok; iz, ileriki "Bildirimlerim" sayfasının ve sorun ayıklamanın temelidir.

**Why this priority**: Kullanıcıya görünür değeri dolaylı; ama gönderimin kanıtı ve genele yayma turunun tohumu.

**Independent Test**: Mail gönderimi sonrası sistem kayıtlarında/mesaj akışında bildirim-gönderildi izi doğrulanır.

**Acceptance Scenarios**:

1. **Given** başarılı mail gönderimi, **When** akış tamamlanır, **Then** hangi kullanıcıya hangi ürün için gönderildiğini söyleyen iz yayınlanır.

---

### Edge Cases

- Fiyat değişir ama üründe hiç alarm yok: hiçbir şey olmaz (boş tetik yayınlanmaz).
- Fiyat değişmeden başka künye alanı değişir: tetik yok (yalnız fiyat değişimi tetikler).
- Mail altyapısı geçici çalışmaz: gönderim yeniden denenir; kalıcı hatada hata kuyruğuna düşer (moderasyon worker'ı deseni). Aynı fiyat değişimi için bilinçli mükerrer mail üretilmez (yeniden-deneme nadir durumda çift maile yol açabilir, kabul).
- Ürün yayından kalkar: alarm sessizce durur (fiyat düşüş tetiği gelmez); kayıt silinmez.
- Kullanıcının e-postası sistemde yoksa/boşsa: gönderim atlanır, iz "gönderilemedi" olarak düşer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Giriş yapmış kullanıcı, ürün detayından o ürüne fiyat alarmı kurabilmeli ve kaldırabilmelidir; anonim kullanıcı düğmede giriş akışına yönlenir (girişten sonra detaya dönülür).
- **FR-002**: Bir kullanıcının aynı ürüne en çok BİR açık alarmı olabilir; tekrar kurma denemesi ikinci kayıt üretmez.
- **FR-003**: Sistem, bir ürünün fiyatı DEĞİŞTİĞİNDE (artış ya da düşüş) o ürüne alarmı olan her kullanıcı için bildirim sürecini tetiklemelidir; fiyat-dışı değişiklikler tetiklemez.
- **FR-004**: Alarm YAŞAYAN bir aboneliktir: kullanıcı kaldırana dek açık kalır ve her fiyat değişiminde yeni mail üretir. Gürültü kontrolü kullanıcıdadır (rahatsız olan alarmı kaldırır); throttle v1 kapsam dışı.
- **FR-005**: Gönderilen mail kişiselleştirilmiş olmalıdır: kullanıcıya hitap, ürün adı, eski ve yeni fiyat, ürün detayına bağlantı içerir; dili Türkçedir.
- **FR-006**: Mail, kullanıcının kayıtlı e-posta adresine gider; adres yoksa gönderim atlanır ve iz "gönderilemedi" olarak kaydedilir.
- **FR-007**: Her gönderim denemesinin sonucu (gönderildi/gönderilemedi; kullanıcı + ürün) sistemde iz bırakmalıdır.
- **FR-008**: Geçici gönderim hataları yeniden denenir; kalıcı hata insan inceleyebilecek bir hata kuyruğunda birikir (mevcut moderasyon worker'ı davranışıyla aynı).
- **FR-009**: Geliştirme ortamında gönderilen mailler gerçek posta hesabı olmadan, yerel bir posta görüntüleyicisinde incelenebilir olmalıdır.

### Key Entities

- **Fiyat Alarmı**: Kullanıcının bir ürünün fiyat değişimini izleme kaydı — kullanıcı, ürün, kuruluş anındaki fiyat; kaldırılana dek yaşar. Yeni "kitaplık" alanının ilk kavramı; favori/listeler ileride aynı alana gelir.
- **Fiyat Değişim Tetiği**: "Bu kullanıcının izlediği ürünün fiyatı değişti" duyurusu — kullanıcı, ürün adı, eski/yeni fiyat; mail üretimine yetecek bilgiyi kendisi taşır.
- **Bildirim İzi**: Gönderim sonucunun kaydı — kullanıcı, ürün, sonuç, zaman.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Alarm kurma tek tıktır (login'liyken); kurulum sonrası durum sayfada anında görünür.
- **SC-002**: Fiyat değişiminden sonra mail, olağan koşullarda 1 dakika içinde kullanıcının kutusunda görünür.
- **SC-003**: Her fiyat değişimi alarm başına EN ÇOK bir mail üretir; aynı değişim için bilinçli mükerrer mail sıfırdır.
- **SC-004**: Fiyat-dışı değişiklikler ve alarmı kaldırılmış kullanıcılar hiçbir mail üretmez.
- **SC-005**: Gönderilen her mailin izi sistemde bulunur; canlı doğrulamada mail içeriği (ad, iki fiyat, bağlantı) birebir kontrol edilir.

## Assumptions

- Kullanıcının mail adresi = üyelik kayıt e-postası; ayrı "bildirim adresi" yönetimi kapsam dışı.
- Mail içeriğini üreten akıl kişiselleştirme için yapay zekâ kullanır; içerik üretilemezse sade bir yedek şablonla gönderim yine yapılır (mail hiç gitmemesinden iyidir).
- Alarm v1 yaşayan abonelik, kural filtresi yok (2026-09-02 kullanıcı kararı; tek-atımlık tasarım terk edildi). "Gürültü kesici (throttle) + başka bildirim türleri + haftalık özet" bilinçli olarak SONRAKİ feature'a bırakıldı.
- "Bildirimlerim" ekranı v1'de yok; yalnız iz bırakılır.
- Kullanıcı bazlı mail izni/abonelikten çıkma (unsubscribe) v1 kapsam dışı — alarm zaten kullanıcının açık talebidir; kaldırmak = alarmı silmek.
- Geliştirmede mailler yerel görüntüleyicide kalır; gerçek dünyaya mail çıkışı yok.