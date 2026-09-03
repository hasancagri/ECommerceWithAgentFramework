# Feature Specification: Dış Agent MCP Erişimi (OAuth)

**Feature Branch**: `061-external-mcp-oauth`

**Created**: 2026-09-03

**Status**: Draft

**Input**: User description: "Dış agent MCP erişimi OAuth ile (Dilim 1): Kullanıcının kendi AI agent'ı
(Claude Code / Claude Desktop) mağazanın MCP uçlarına OAuth 2.1 ile bağlanıp yazışmayla uçtan uca
alışveriş yapabilsin. Bir kerelik tarayıcı OAuth handoff, refresh token ile sonrası tamamen ekransız."

**Kademe**: Tam — yeni dış yüzey kontratı (OAuth keşif metadata'sı), MCP açan tüm servisleri ve
Identity.Server'ı kapsayan servisler-arası etki. (Yeni aggregate/tablo yok; domain-TDD kapsamı dar.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mevcut kullanıcı kendi agent'ıyla alışveriş yapar (Priority: P1)

Ayşe'nin mağazada hesabı var. Claude Code/Desktop'ına mağazanın MCP adresini ekler; agent onu bir
kez tarayıcıya yönlendirir, Ayşe mevcut login sayfasında oturum açar ve istenen izinleri onaylar.
Sonrasında tamamen yazışarak kitap arar, sepete atar, sipariş verir ve siparişini görüntüler —
hiçbir mağaza ekranı açmadan.

**Why this priority**: Feature'ın varlık sebebi; "ekransız alışveriş" vizyonunun ilk canlı kanıtı.
Tek başına MVP: bu akış çalışıyorsa dilim başarılı.

**Independent Test**: Claude Code'dan localhost gateway'ine bağlan; OAuth'u tamamla; chat'ten
arama→sepet→sipariş→sipariş görüntüleme zincirini uçtan uca yürüt.

**Acceptance Scenarios**:

1. **Given** kimliği olmayan bir MCP isteği, **When** agent mağaza MCP ucuna bağlanır, **Then**
   sistem kimlik gerektiğini ve kimlik sağlayıcının nereden keşfedileceğini standart biçimde bildirir.
2. **Given** keşif bilgisini alan agent, **When** kullanıcı bağlantıyı başlatır, **Then** tarayıcıda
   mevcut login sayfası açılır; login + izin onayı sonrası agent erişim yetkisi kazanır.
3. **Given** bağlı bir agent, **When** kullanıcı "kitap ara / sepete at / sipariş ver / siparişimi
   göster" der, **Then** her adım kullanıcının kimliğiyle çalışır ve sonuç yazışmayla döner.
4. **Given** bağlı bir agent, **When** kullanıcı izin verilmeyen bir alana dokunan tool çağrısı
   tetikler, **Then** çağrı yetki hatasıyla reddedilir (sessiz başarısızlık yok).

---

### User Story 2 - Yeni kullanıcı bağlanırken kayıt olur (Priority: P2)

Deniz'in hesabı yok. Agent bağlantısının açtığı tarayıcı sayfasındaki kayıt bağlantısından hesap
oluşturur (mevcut kayıt akışı, otomatik `customer` rolü) ve aynı seremoni içinde izin onayına
devam eder; sonrasında P1'deki gibi ekransız alışveriş yapar.

**Why this priority**: Yeni kullanıcının kanala girişi; P1'in üstünde tek ek adım (mevcut register
sayfası) olduğundan ayrı ama küçük bir hikâye.

**Independent Test**: Temiz kullanıcıyla bağlantı başlat, tarayıcıda register'ı tamamla, dönüşte
alışveriş zincirinin çalıştığını doğrula.

**Acceptance Scenarios**:

1. **Given** hesabı olmayan kullanıcı, **When** bağlantı seremonisinde kayıt olur, **Then** hesap
   `customer` rolüyle oluşur ve aynı akış kesintisiz izin onayına ilerler.

---

### User Story 3 - Sonraki oturumlar ekransız; mevcut kanallar bozulmaz (Priority: P3)

Ayşe ertesi gün agent'ından devam eder: hiçbir login ekranı görmez (yetki sessizce yenilenir).
Aynı anda WebApp, ChatAgent ve UserKey ile bağlanan mevcut akışlar önceden olduğu gibi çalışır.

**Why this priority**: Kalıcılık + regresyon güvencesi; P1 çalışmadan anlamı yok.

**Independent Test**: Erişim süresi dolduktan sonra tool çağrısının ekransız başarılı olduğunu
gözle; ChatAgent'la sipariş akışını ve UserKey'li MCP çağrısını yeniden koş (canlı PASS).

**Acceptance Scenarios**:

1. **Given** süresi dolmuş erişim yetkisi, **When** agent yeni tool çağrısı yapar, **Then** yetki
   kullanıcıya ekran gösterilmeden yenilenir ve çağrı başarılı olur.
2. **Given** mevcut ChatAgent/WebApp/UserKey akışları, **When** bu feature devreye girer, **Then**
   hepsi davranış değişikliği olmadan çalışmaya devam eder.

---

### Edge Cases

- Kullanıcı izin (consent) ekranında reddederse: agent erişim alamaz; mağazada hiçbir kayıt/yan
  etki oluşmaz; kullanıcı daha sonra yeniden deneyebilir.
- Kullanıcı bağlantıyı koparırsa (disconnect/revoke): agent'ın sonraki çağrıları kimlik hatası
  alır; yeniden bağlanma aynı seremoniyle mümkün.
- Aynı kullanıcı birden çok servis MCP'sine bağlanır (servis-başına connector): kimlik sağlayıcı
  oturumu sayesinde login bir kez; her bağlantının kendi izin onayı olabilir.
- Yetki kapsamı dışındaki tool çağrısı: açık yetki hatası döner; agent kullanıcıya durumu söyler.
- Kimliksiz istekte keşif bilgisi dönerken mevcut UserKey isteği etkilenmez (iki kimlik yolu
  yan yana yaşar).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kimliksiz MCP istekleri, dış agent'ların kimlik sağlayıcıyı otomatik keşfetmesini
  sağlayan standart bir yanıtla (keşif adresi taşıyan kimlik-gerekli yanıtı) reddedilMELİdir.
- **FR-002**: MCP ucu açan her servis, korunan-kaynak keşif bilgisini standart bir adresten
  sunMALIdır; bu yetenek tek noktada tanımlanıp her serviste etkinleştirilir.
- **FR-003**: Kimlik sağlayıcı, kullanıcının kendi agent'ını (Claude Code/Desktop) bilinen bir
  istemci olarak tanıMALI; bağlantı tarayıcı üzerinden güvenli yetki akışıyla (PKCE'li) kurulMALIdır.
- **FR-004**: Login ve kayıt, kimlik sağlayıcının MEVCUT sayfalarıyla yapılMALIdır; kayıt olan
  kullanıcı mevcut davranışla `customer` rolü alır. Yeni login/register ekranı üretilmez.
- **FR-005**: Kullanıcı, agent'a verilecek izinleri bağlantı sırasında görüp onaylayabilMELİ ya da
  reddedebilMELİdir; onaysız erişim verilemez.
- **FR-006**: Dış agent'a tanınacak izin kümesi, alışveriş yaşam döngüsüyle sınırlı kapalı bir
  demet olMALIdır (arama, ürün, sepet, sipariş, müşteri profili okuma); yönetim yüzeyleri kapsam dışı.
- **FR-007**: MCP uçları, agent'ın taşıdığı kullanıcı kimliğini (JWT Bearer) doğrulayıp yetkiyi
  scope bazında zorlaMALIdır (İlke V); mevcut UserKey yolu davranış değişmeden çalışmaya devam eder.
- **FR-008**: Erişim süresi dolduğunda yetki, kullanıcıya ekran gösterilmeden yenilenebilMELİdir
  (bir kerelik seremoni garantisi; refresh token).
- **FR-009**: Kullanıcı bağlantıyı kopardığında verilmiş yetkiler geçersiz kılınabilMELİdir.
- **FR-010**: Uçtan uca akış canlı doğrulanMALIdır: Claude Code'dan localhost'a bağlan, OAuth'u
  tamamla, yazışmayla ara→sepete at→sipariş ver→siparişi görüntüle.

### Key Entities

- **Dış agent istemci kaydı**: Kullanıcının agent'ını temsil eden bilinen istemci tanımı;
  yönlendirme adresleri ve izinli scope demeti ile sınırlı.
- **Yetki (erişim + yenileme token'ları)**: Bağlantı seremonisinde verilen, süreli ve iptal
  edilebilir erişim; agent tarafında saklanır, sohbet içeriğine asla girmez.
- **İzin (consent) kararı**: Kullanıcının hangi agent'a hangi kapsamda izin verdiğinin kaydı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Hesabı olan kullanıcı, agent'ını 5 dakikadan kısa sürede bağlar (tek tarayıcı
  seremonisi dahil) ve ilk aramasını yazışmayla tamamlar.
- **SC-002**: Bağlantı sonrası alışveriş yaşam döngüsü (arama→sepet→sipariş→takip) %100 yazışmayla
  tamamlanır; hiçbir mağaza ekranı açılmaz.
- **SC-003**: İkinci ve sonraki kullanım oturumlarında kullanıcı hiçbir login/izin ekranı görmez.
- **SC-004**: Mevcut kanallar (WebApp, ChatAgent, UserKey'li MCP) regresyonsuz: mevcut canlı akış
  senaryoları değişiklik sonrası aynen PASS.
- **SC-005**: İzin reddi ve bağlantı koparma senaryoları yan-etkisiz ve açık hata mesajıyla
  sonuçlanır (sessiz başarısızlık yok).

## Assumptions

- Dış agent izin demeti, mağaza-içi asistanın (ChatAgent ASSISTANT personası) kullandığı alışveriş
  yüzeyleriyle eşleşir; kesin scope listesi plan aşamasında `KnownScopes`'tan seçilir (İlke V:
  kapalı registry, serbest metin yok).
- Kimlik taşıyıcı JWT Bearer'dır (OAuth çıktısı); UserKey bu akışta KULLANILMAZ, mevcut haliyle
  yan yolda yaşar. UserKey'in uzun vadeli geleceği ayrı karar (Dilim 2 tartışması).
- İlk canlı hedef localhost + Claude Code'dur; public erişim (tünel + claude.ai/ChatGPT connector)
  bu dilimin kapsamı dışındadır.
- Topoloji servis-başına MCP'dir (mevcut gateway `/mcp/<servis>` yolları); birleşik tek uç ayrı
  feature'dır. Chat-içi kayıt/OTP ve ACP protokolü kapsam dışıdır (yol haritası Dilim 2/3).
- Kimlik sağlayıcının mevcut login/register sayfaları ve rol→scope mekanizması (İlke V) aynen
  kullanılır; bu dilim yeni kimlik ekranı üretmez (istisna: izin-onay ekranı mevcutta yoksa
  eklenir, tek sayfa).
- Agent tarafında yetki saklama/yenileme agent platformunun (Claude) sorumluluğudur; mağaza
  tarafı standartlara uyumlu davranmakla yükümlüdür.
- Ödeme mevcut mock davranışıyla sürer (tutar bazlı); kart ekleme bu dilimin konusu değildir.
- Ürün görseli: tool yanıtındaki mevcut görsel adresi (URL) yeterlidir; görselin serve edilmesi /
  onarımı kapsam dışıdır (bilinen durum: serving yok).