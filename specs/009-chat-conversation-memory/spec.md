# Feature Specification: ChatAgent Kalıcı Konuşma Memory'si

**Feature Branch**: `009-chat-conversation-memory`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "ChatAgent'ta yazışmalar için kalıcı memory: login kullanıcı geçmiş
konuşmalarını süresiz görür ve devam ettirir; anonim kullanıcı aynı oturumda sürekliliğini korur;
modele her turda yalnız son N item gider (depo eksiksiz kalır)."

**Kademe**: Tam — yeni veritabanı ve yeni uç kontratları var; tam akış işletilir (plan dahil).
Belirsizlikler brainstorming'de giderildi (kapsam, anonim davranışı, pencere).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sohbet kalıcıdır, kopmaz (Priority: P1)

Giriş yapmış kullanıcı olarak sohbetimin sunucu yeniden başlasa da, günler sonra dönsem de
kaldığı yerden sürmesini istiyorum; yazdıklarım kaybolmamalı.

**Why this priority**: Feature'ın varlık sebebi; bugün geçmiş uçucu bellekte ve restart'ta siliniyor.

**Independent Test**: Sohbet başlat, birkaç mesajlaş, sistemi yeniden başlat; aynı konuşma
kimliğiyle devam et — asistan önceki bağlamı bilir, mesajlar durur.

**Acceptance Scenarios**:

1. **Given** süren bir sohbet, **When** sistem yeniden başlar ve kullanıcı devam eder,
   **Then** sohbet aynı bağlamla sürer; hiçbir mesaj kaybolmaz.
2. **Given** sohbette asistan sepete ürün ekledi (araç çağrısı), **When** sohbete sonra dönülür,
   **Then** o adımlar da bağlamın parçasıdır.
3. **Given** konuşma deposu erişilemez, **When** kullanıcı mesaj yazar,
   **Then** açık bir hata görünür; sistem sessizce uçucu belleğe düşmez.

---

### User Story 2 - Geçmiş konuşmalar listesi (Priority: P2)

Giriş yapmış kullanıcı olarak geçmiş konuşmalarımı listede görmek, birini açıp tüm mesajlarıyla
okumak, istersem oradan devam etmek ya da yeni sohbet başlatmak istiyorum.

**Why this priority**: Kalıcılığın kullanıcıya görünen değeri; süresiz erişim buradan yaşanır.

**Independent Test**: Farklı günlerde birkaç sohbet aç; listede hepsini gör, eskisini aç —
tüm mesajlar eksiksiz gelir; devam et veya yeni sohbet başlat.

**Acceptance Scenarios**:

1. **Given** birden çok geçmiş konuşma, **When** kullanıcı chat panelini açar,
   **Then** konuşmalar son aktiviteye göre, anlaşılır başlıklarla listelenir.
2. **Given** listeden eski bir konuşma seçildi, **When** açılır,
   **Then** o konuşmanın TÜM mesajları görüntülenir (görüntülemede kırpma yok).
3. **Given** kullanıcı yeni sohbet başlattı, **When** mesajlaşır,
   **Then** eski konuşmalar değişmez; yeni konuşma listeye eklenir.
4. **Given** aradan aylar geçti, **When** kullanıcı yeniden giriş yapar,
   **Then** geçmiş konuşmaları hâlâ listededir (login kullanıcıda otomatik silme yoktur).

---

### User Story 3 - Anonim oturum sürekliliği (Priority: P3)

Anonim ziyaretçi olarak aynı tarayıcı oturumu içinde sohbetimin bağlamını korumasını istiyorum;
oturum bitince bu geçmişe erişim beklemem.

**Why this priority**: Bugünkü davranışın korunması; kalıcı liste anonim için kapsam dışı.

**Independent Test**: Anonim sohbet et, sayfayı yenile, devam et — bağlam korunur; konuşma hiçbir
listede görünmez ve süresi dolunca silinir.

**Acceptance Scenarios**:

1. **Given** anonim bir sohbet, **When** aynı oturumda sayfa yenilenir ve devam edilir,
   **Then** bağlam korunur.
2. **Given** anonim bir konuşma, **When** tanımlı süre boyunca aktivite olmaz,
   **Then** konuşma kalıcı depodan silinir; hiçbir kullanıcı listesinde asla görünmez.

---

### Edge Cases

- Başka kullanıcının konuşma kimliği istenirse: "bulunamadı" davranışı — varlığı dahi sızdırılmaz.
- Geçersiz/silinmiş konuşma kimliğiyle devam denenirse: sessizce yeni sohbet başlar, kullanıcıya
  teknik hata sızmaz.
- Çok uzun sohbette (yüzlerce mesaj) modele yalnız son N item gider; kullanıcı ekranda hepsini görür,
  asistan pencere dışındaki detayı hatırlamayabilir (bilinçli sınır).
- Aynı kullanıcı iki sekmede aynı konuşmayı sürdürürse: her tur sunucudaki güncel geçmişe eklenir;
  sıra karışabilir ama kayıt kaybolmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Giriş yapmış kullanıcının konuşmaları kalıcı depoda, kullanıcı sahipliğiyle saklanır.
- **FR-002**: Sohbet bir konuşma kimliğiyle sürdürülür; geçmiş sunucuda yüklenir, tarayıcı geçmişi
  yeniden göndermez.
- **FR-003**: Kullanıcı kendi konuşmalarını son aktiviteye göre sıralı ve sayfalı listeler;
  başlık ilk kullanıcı mesajından türetilir.
- **FR-004**: Geçmiş bir konuşma açıldığında tüm mesajları eksiksiz görüntülenir.
- **FR-005**: Modele giden bağlam son N konuşma öğesiyle sınırlıdır (N yapılandırılır, varsayılan 40);
  depo asla kırpılmaz.
- **FR-006**: Asistanın araç çağrıları ve sonuçları da konuşmanın parçası olarak saklanır.
- **FR-007**: Sahiplik zorunludur: kullanıcı yalnız kendi konuşmalarını listeler/açar/sürdürür;
  başkasınınki "bulunamadı" gibi davranır.
- **FR-008**: Giriş yapmış kullanıcının konuşmaları otomatik silinmez (TTL yok).
- **FR-009**: Anonim konuşma aynı oturumda sürer; hiçbir listede görünmez ve 24 saat aktivitesizlik
  sonunda otomatik silinir.
- **FR-010**: Geçersiz/bayat konuşma kimliğinde sistem yeni sohbet başlatır; akış kırılmaz.
- **FR-011**: Kalıcı depo erişilemezse sohbet açık hata verir; uçucu belleğe sessiz düşüş yoktur.
- **FR-012**: Kullanıcı her an yeni sohbet başlatabilir; mevcut konuşmalar bundan etkilenmez.

### Key Entities

- **Konuşma**: bir sohbet oturumu; sahibi (login kullanıcı ya da anonim), başlığı, hangi agent'la
  yapıldığı, oluşturulma ve son aktivite zamanı.
- **Konuşma öğesi**: konuşma içindeki sıralı kayıt — kullanıcı mesajı, asistan cevabı veya araç
  çağrısı/sonucu.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sistem yeniden başlatıldıktan sonra kullanıcı sohbetine sıfır mesaj kaybıyla devam eder.
- **SC-002**: Kullanıcı aradan ne kadar süre geçerse geçsin (ör. 5 ay) giriş yaptığında geçmiş
  konuşmalarının tamamını listede görür.
- **SC-003**: Hiçbir kullanıcı hiçbir yolla başka kullanıcının konuşmasını göremez (sızıntı: 0).
- **SC-004**: Konuşma listesi ve geçmiş bir sohbetin açılışı 2 saniye içinde tamamlanır.
- **SC-005**: 100+ mesajlık bir sohbette yeni mesajın cevap süresi, kısa sohbetle aynı mertebede
  kalır (bağlam penceresi sayesinde).
- **SC-006**: Anonim ziyaretçi aynı oturumda sayfa yenilese de bağlamını korur; sahipsiz konuşmalar
  24 saat aktivitesizlik sonrası depodan silinmiş olur.

## Canlı Doğrulama (2026-07-24, Aspire — headless)

- SC-001 ✓ tam restart sonrası aynı konuşma bağlamıyla sürdü ("adın Hasan, markan Apple"); kayıp 0.
- SC-002 ✓ mekanizma (kalıcı depo, login'de TTL yok, liste ucu 401-korumalı); liste UI'ının
  tarayıcıdan gezilmesi kullanıcı doğrulamasına bırakıldı.
- SC-003 ✓ token'sız liste/items 401; sahiplik süzgeci kodda tek kapıda. İki-kullanıcı UI turu
  kullanıcıya bırakıldı.
- SC-004/005 kısmî: anon akışta yanıt anlık; pencere mantığı birim testli (8/8). UI ölçümü kullanıcıda.
- SC-006 ✓ TTL süpürücü ilk tikte 30 saat yaşlı sahipsiz konuşmayı sildi, tazeyi korudu.
- FR-010 ✓ bayat id'de BFF yeni konuşma açtı (X-Conversation-Id) ve akış kesilmedi.
- FR-011 ✓ Postgres kapalıyken açık 500; dönünce aynı konuşma bağlamıyla devam.
- Bonus bulgu: Marten tablo adı locale'le küçülüyordu (tr'de "ıtem") → `DocumentAlias` ile sabitlendi.

## Assumptions

- Tek istemci WebApp'teki mevcut chat widget'ıdır; mobil/başka istemci kapsam dışı.
- Bağlam penceresi varsayılanı 40 öğedir; yapılandırmayla değiştirilebilir.
- Anonim süreklilik tarayıcı oturumuna bağlıdır; oturum/çerez silinirse bağlam kaybı beklenen davranıştır.
- Konuşma silme ucu, semantik hatırlama/özetleme, konuşma içi arama ve dışa aktarma kapsam dışıdır.
- Kalıcı depo diğer servislerinkiyle aynı dayanıklılık varsayımlarını paylaşır (kalıcı volume).