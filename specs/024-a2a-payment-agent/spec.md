# Feature Specification: ChatAgent A2A Installment Quote

**Feature Branch**: `024-a2a-payment-agent`

**Created**: 2026-08-02

**Status**: Draft

**Artefakt kademesi**: **Tam** — ayrı bir uygulamada yaşayan uzak agent'a yeni bir
ajan-ajan (A2A) entegrasyon kontratı ve dış bağımlılığa karşı graceful-degrade davranışı
getirir. Şüphede üst kademe seçildi.

**Input**: E-ticaret sohbet asistanının (assistant), ayrı solution'daki (`PaymentGateway`)
uzak bir A2A PaymentAgent'a Agent2Agent protokolüyle bağlanıp **sepet için taksit
seçeneklerini** sorgulaması.

## Kapsam ve Sınır

**Kapsamda (024):** Yalnız **taksit sorgulama**. Kullanıcı sohbette "default kartımla
sepetteki tutar için taksitleri getirir misin?" der; asistan sepet toplamını + default
kartın **BIN**'ini (ilk 6 hane) bulur, uzak A2A PaymentAgent'a danışır ve o bankaya özel
taksit seçeneklerini listeler. Read-only; **PAN/CVV/token veya ödeme işlemi (charge) YOK**.

**BIN neden kapsamda, PAN neden değil:** Taksit kampanyası **banka**ya bağlıdır; bankayı
BIN (ilk 6 hane) belirler. BIN PCI'a göre **hassas değildir** (kartı değil, kart-ürünü
tanımlar) — açık gönderilebilir. Ortadaki haneler, CVV ve tam PAN **asla** A2A/LLM'e girmez.

**Kapsam dışı (ayrı feature'lar):**
- **Ödeme / çekim (charge):** kayıtlı kartla veya form ile ödeme. Sonraki feature.
- Kart ekleme / tokenizasyon.
- Resmî "ödeme bekleyen sipariş" durum makinesi.
- Uzak A2A PaymentAgent'ın **sunucu tarafı** (ayrı solution'da geliştirilir).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sohbetten taksit seçeneklerini öğrenme (Priority: P1)

Giriş yapmış kullanıcı sepetini doldurduktan sonra asistana "sepetteki ürünler için
taksitleri getirir misin?" der. Asistan sepet toplamını bulur, uzak ödeme agent'ına
danışır ve banka/taksit-sayısı/taksit-tutarı/komisyon içeren seçenekleri listeler.

**Why this priority**: Feature'ın asıl değeri budur; kart datası içermez, tek başına
kullanıcıya değer verir (ödemeden önce maliyeti görme) ve A2A istemci hattını uçtan uca
kanıtlar.

**Independent Test**: Sepette ürün olan kullanıcı taksit sorar; asistan uzak agent'tan
gelen taksit tablosunu sepet toplamıyla tutarlı biçimde listeler. Sepet boşsa uyarılır.

**Acceptance Scenarios**:

1. **Given** sepetinde ürün bulunan giriş yapmış kullanıcı, **When** "sepetteki ürünler
   için taksitleri getir" der, **Then** asistan sepet toplamına karşılık gelen taksit
   seçeneklerini (banka, taksit sayısı, taksit başına tutar, komisyon) listeler.
2. **Given** sepeti boş kullanıcı, **When** taksit sorar, **Then** asistan önce sepete
   ürün eklemesi gerektiğini söyler ve uzak agent'ı çağırmaz.
3. **Given** kullanıcı ödeme/çekim ister ("öde", "satın al"), **When** bunu söyler,
   **Then** asistan bu yeteneğin henüz kapsamda olmadığını nazikçe belirtir (yalnız taksit
   bilgisi verir).

---

### User Story 2 - Uzak agent yokken güvenli çalışma (Priority: P2)

Uzak ödeme agent'ı henüz yayında değilken veya geçici erişilemezken, asistanın geri kalan
tüm yetenekleri (arama, sepet, sipariş vb.) etkilenmeden çalışır; taksit niyeti nazik bir
"şu an kullanılamıyor" mesajıyla karşılanır.

**Why this priority**: Uzak agent ayrı solution'da ve bu feature'la eşzamanlı geliştirilecek;
bağımlılığın yokluğu asistanı bozmamalı. Dayanıklılık dilimidir.

**Independent Test**: Uzak agent adresi tanımsız/erişilemezken asistan başlatılır; ürün
arama ve sepet akışları çalışır, taksit niyeti düzgün degrade olur.

**Acceptance Scenarios**:

1. **Given** uzak agent yapılandırılmamış, **When** asistan başlatılır, **Then** asistan
   sorunsuz açılır ve taksit-dışı tüm yetenekler çalışır.
2. **Given** uzak agent erişilemez, **When** kullanıcı taksit ister, **Then** asistan
   bilginin şu an alınamadığını bildirir; hata/exception sızdırmaz, sohbet çökmez.

---

### Edge Cases

- Uzak agent kısmi/biçimsiz taksit yanıtı dönerse: asistan tutarlı bilgiyi listeler,
  eksikse durumu bildirir; alan **uydurmaz**.
- Sepet toplamı alınamazsa (sepet servisi hatası): asistan taksit sorgusunu yapmaz ve
  durumu kullanıcıya bildirir.
- Uzak agent hiç taksit seçeneği dönmezse (ör. tutar çok düşük): asistan "uygun taksit
  seçeneği yok" biçiminde net bilgi verir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Assistant, taksit sorgulama niyetini ("sepetteki taksitler") diğer niyetlerden
  ayırt edip bu niyeti uzak ödeme agent'ına delege edebilmelidir.
- **FR-002**: Assistant, taksit sorgusundan önce sepet toplamını mevcut sepet yeteneğiyle
  belirlemeli ve tutarı uzak agent'a taksit sorgusunun girdisi olarak vermelidir.
- **FR-002a**: Assistant, kullanıcının **default kartının BIN'ini** (ilk 6 hane) Customer
  BC'den okuyup tutarla birlikte uzak agent'a vermelidir. Default kart yoksa BIN'siz (genel)
  sorgu yapar veya kullanıcıdan kart eklemesini ister (bkz. Edge Cases).
- **FR-002b**: Customer BC saved-card, kart eklenirken **BIN'i (ilk 6 hane) yakalayıp
  saklamalıdır**. BIN hassas değildir; PAN/CVV **saklanmaz**. (Küçük Customer BC değişikliği.)
- **FR-003**: Assistant, taksit yanıtını (banka, taksit sayısı, taksit başına tutar,
  toplam/komisyon) anlaşılır biçimde listelemeli; alan uydurmamalıdır.
- **FR-004**: Sepet boşsa asistan uzak agent'ı çağırmamalı, önce sepete ürün eklenmesini
  istemelidir.
- **FR-005**: Bu feature **yalnız okuma/sorgudur**; **PAN, CVV, token veya ödeme işlemi
  (charge)** yapılmaz/gönderilmez. Yalnız hassas-olmayan BIN + tutar delege edilir. Ödeme
  niyetleri kapsam-dışı olarak nazikçe geri çevrilir.
- **FR-006**: Uzak agent yapılandırılmamış veya erişilemez olduğunda asistan **çökmeden**
  başlamalı ve çalışmalı; taksit niyeti nazik bir "şu an kullanılamıyor" yanıtıyla degrade
  olmalı, teknik hata/exception kullanıcıya sızmamalıdır.
- **FR-007**: Uzak agent'ın kimliği (AgentCard) ve delege edilen yetenek adı (taksit
  sorgulama) **kontrat olarak sabitlenmeli**; uzak taraf ayrı solution'da geliştirildiği
  için bu isimler önceden kararlaştırılmış olmalıdır.
- **FR-008**: **Şimdilik merchant key / kullanıcı token'ı uzak agent'a GÖNDERİLMEZ** (uzak
  taraf henüz yok; auth iletimi ertelendi). Çağrı yine **kendi HttpClient**'ımızla yapılır
  (SSE resilience-muafiyeti için zorunlu). Auth handler'ı ileride eklenecek genişleme noktası
  olarak bırakılır; eklendiğinde yetki **scope-tabanlı** kalır (rol yok). PAN/CVV/token asla.

### Key Entities *(include if data involved)*

- **Uzak Ödeme Agent'ı (A2A PaymentAgent)**: Ayrı uygulamada yayınlanan, merchant kapsamlı,
  taksit sorgulama yeteneği sunan uzak ajan. Bu sistemin dışında yaşar; bu feature onu bir
  kontrat üzerinden **tüketir**, sahiplenmez.
- **Taksit Seçeneği (görüntüleme verisi)**: Banka, taksit sayısı, taksit başına tutar,
  toplam/komisyon. Kalıcı değil; uzak agent yanıtından türetilip kullanıcıya gösterilir.
- **Sepet Toplamı (girdi)**: Mevcut sepet yeteneğinden alınan, taksit sorgusunun tek girdisi.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı, sepeti doluyken tek bir doğal-dil mesajıyla taksit seçeneklerini
  görebilir; gösterilen taksit tutarları sepet toplamıyla tutarlıdır.
- **SC-002**: Uzak agent kapalıyken asistan başlatılır ve taksit-dışı akışların tamamı
  (arama, sepet, sipariş) çalışır; taksit niyeti anlaşılır bir "şu an kullanılamıyor"
  mesajı döner (çökme/teknik hata yok).
- **SC-003**: Taksit-dışı hiçbir niyet uzak ödeme agent'ına gönderilmez (yalnız taksit
  sorgusu delege edilir).

## Assumptions

- **Girdi = sepet toplamı + default kart BIN**: Taksit banka-özeldir; bankayı kartın BIN'i
  (ilk 6 hane, hassas değil) belirler. Sonuç kullanıcının **kendi kartının bankasının** taksit
  tablosudur — bankalar-arası "en ucuz" kıyası DEĞİL (kart issuer'ından ödenir). Default kart
  yoksa BIN'siz genel sorgu (fallback) ya da kart eklemesi istenir. PAN/CVV asla gönderilmez.
- **Uzak agent teknolojisi**: Uzak taraf A2A protokolüyle erişilebilir bir agent yayınlar;
  yetenek adı ve girdisi bu feature ile eşzamanlı kararlaştırılır (FR-007).
- **Kimlik/yetki**: Mevcut kullanıcı token akışı ve scope-tabanlı yetki modeli yeniden
  kullanılır; yeni rol modeli getirilmez.
- **Ödeme sonraki feature**: Bu spec ödemeyi kapsamaz; taksit seçeneklerini yalnız gösterir.

## Dependencies

- Uzak **A2A PaymentAgent** (ayrı solution `PaymentGateway`) — bu feature onu tüketir; uzak
  taraf henüz yok, kontrat-önce yaklaşımıyla yetenek adı sabitlenir, hazır olana kadar
  asistan graceful-degrade eder (US2).
- Mevcut sepet okuma yeteneği (sepet toplamı için).
- Customer BC saved-card (022 Wallet) — default kart + **BIN**. BIN bugün saklanmıyor;
  bu feature `AddCard`'da BIN yakalama + `SavedCard.Bin` alanı ekler (küçük değişiklik).
  Gerçek kart vault'u (envelope PAN, tokenize/charge) AYRI/ileri feature — burada kapsam dışı.