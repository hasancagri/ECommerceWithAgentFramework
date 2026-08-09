# Feature Specification: Admin — Metinle Merchant Onboarding (Gateway Merchant.Agent A2A)

**Feature Branch**: `032-merchant-onboarding-a2a-admin`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "ECommerce'in PaymentGateway'in Merchant.Agent'ına A2A üzerinden bağlanması. Yalnız yönetimin (admin) kullanacağı bir metin ekranından, doğal dille merchant onboarding başvurusu (register) ve durum sorgusu (registration_status) yapılabilsin. PaymentAgentInstallmentTool deseniyle yeni bir A2A tool; ayrı `admin` agent persona'sı; ekran RBAC ile admin-korumalı. Mevcut yapısal MCP yolu (GatewayRegistrationClient) yan yana kalır."

## Problem

Bugün ECommerce, DropShop gateway'e onboarding başvurusunu yalnız **yapısal MCP** ile yapıyor
(`GatewayRegistrationClient` → Merchant.Api `/mcp submit_registration`, `POST
/gateway-onboarding/register` tetiği). Bu deterministik ama **metin/sohbet** girişi yok; gateway'in
**Merchant.Agent** A2A host'u (agent-card + LLM router, `register`/`registration_status` skill'leri)
hiçbir ECommerce istemcisi tarafından tüketilmiyor.

Yönetim, doğal dille ("shop.example.com'u gateway'e kaydet", "başvurum ne durumda?") onboarding
yürütmek istiyor. Bu feature, ECommerce'e **admin-only bir metin (chat) yüzeyi** ekler; bu yüzey
gateway'in Merchant.Agent'ına **A2A** ile bağlanır (mevcut taksit tool'u `PaymentAgentInstallmentTool`
deseni). Shopper asistanına dokunulmaz; yapısal MCP yolu otomasyon için korunur.

## Clarifications

### Session 2026-08-09

- Q: A2A yolu mevcut yapısal MCP kaydını (GatewayRegistrationClient) değiştirsin mi? → A: Yan yana
  dursun (coexist). A2A metin yolu **eklenir**; yapısal MCP GatewayRegistrationClient otomasyon için kalır.
- Q: Giriş noktası nerede? → A: ChatAgent'a **ayrı `admin` agent persona'sı** (public/assistant gibi
  üçüncü persona) + yalnız yönetimin eriştiği korumalı metin ekranı. Onboarding tool'ları yalnız bu
  agent'ta; shopper asistanına eklenmez (persona ayrık, mimariye uygun — agent'lar boot'ta singleton).
- Q: Hangi skill'ler? → A: `register` + `registration_status` (ikisi de).
- Q: Admin-gating nerede? → A: Ekran/route RBAC ile (`User.IsInRole("admin")`, 030) + agent seçimi;
  tool-içi rol kontrolü YOK. Gateway'e giden asıl çağrı makine kimliğiyle (`ecommerce-onboarding`
  client_credentials) gider — admin kullanıcının token'ıyla değil.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin metinle başvuru yapar (Priority: P1)

Admin rolündeki bir yönetici, admin-korumalı onboarding ekranındaki metin kutusuna doğal dille
başvuru yazar (ör. "shop.example.com'u gateway'e kaydet"). Mesaj `admin` agent'a gider; agent
gateway Merchant.Agent'ına A2A ile bağlanır, LLM router `register` skill'ini seçer, başvuru açılır ve
sonuç (durum + varsa challenge adımı) metinle döner.

**Why this priority**: Feature'ın çekirdeği — metinle onboarding. Tek başına teslim edilebilir
(yalnız register) ve değer üretir.

**Independent Test**: Admin ekranına "X sitesini kaydet" yaz → admin agent A2A ile Merchant.Agent'ı
çağırır → başvuru açılır, yanıt metinle döner. Admin olmayan kullanıcı ekrana/endpoint'e erişemez.

**Acceptance Scenarios**:

1. **Given** admin rolünde giriş yapmış kullanıcı, **When** onboarding ekranına doğal dille başvuru
   yazar, **Then** `admin` agent Merchant.Agent'ı A2A ile çağırır, `register` skill'i tetiklenir ve
   başvuru sonucu (durum + sıradaki adım) metinle döner.
2. **Given** gateway Merchant.Agent erişilemez/agent-card alınamaz, **When** admin başvuru yazar,
   **Then** ekran çökmez; kullanıcıya durumun alınamadığı nazikçe metinle bildirilir (fail-open,
   `PaymentAgentInstallmentTool` graceful-degrade deseni).
3. **Given** admin OLMAYAN (veya anonim) kullanıcı, **When** onboarding ekranına/endpoint'ine
   erişmeye çalışır, **Then** RBAC ile reddedilir (ekran görünmez / endpoint 403).

---

### User Story 2 - Admin başvuru durumunu sorar (Priority: P2)

Admin, aynı metin ekranından "shop.example.com başvurum ne durumda?" yazar; `admin` agent
Merchant.Agent'ın `registration_status` skill'ini seçer ve güncel durum + sıradaki adım metinle döner.

**Why this priority**: Başvuruyu tamamlayan takip yeteneği; register'dan bağımsız test edilebilir.

**Independent Test**: Var olan bir domain için "durumu ne?" yaz → `registration_status` çağrılır →
durum metni döner.

**Acceptance Scenarios**:

1. **Given** daha önce açılmış bir başvuru, **When** admin domain ile durum sorar, **Then**
   `registration_status` tetiklenir ve güncel durum (AwaitingDomainControl/Pending/Approved/Rejected)
   + sıradaki adım metinle döner.
2. **Given** hiç başvurusu olmayan domain, **When** admin durum sorar, **Then** "başvuru bulunamadı"
   benzeri açık metin döner (hata gibi gösterilmez).

---

### Edge Cases

- **Persona sızıntısı**: `admin` agent yalnız onboarding tool'larını taşır; shopper araçları (sepet,
  ürün, taksit) burada OLMAZ ve shopper asistanına onboarding tool'u SIZMAZ.
- **Config eksik**: Merchant.Agent A2A url yoksa onboarding tool eklenmez (graceful-degrade); admin
  ekranı yine açılır ama "onboarding şu an kullanılamıyor" der.
- **Yetki**: Ekran/route yalnız `admin` rolüne açık; token yoksa/anonimse erişim yok.
- **Gateway auth**: A2A ile gateway'e giden asıl işlem makine kimliğiyle (client_credentials); admin
  kullanıcı token'ı gateway'e taşınmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: ECommerce, gateway **Merchant.Agent**'ına **A2A** ile bağlanan bir onboarding tool'u
  SUNMALIDIR; bağlanma `PaymentAgentInstallmentTool` deseniyle olur (agent-card resolve → skill
  doğrula → `AsAIFunction`). Uzak taraf yok/erişilemezse fail-open (tool eklenmez, boot çökmez).
- **FR-002**: Onboarding tool'u Merchant.Agent'ın **`register`** ve **`registration_status`**
  skill'lerini kullanmalıdır (agent-card'da skill doğrulaması; yoksa ilgili tool eklenmez).
- **FR-003**: ChatAgent'ta **`admin`** adında üçüncü bir agent persona'sı (public/assistant deseni,
  `AddAIAgent("admin")`) OLMALIDIR; onboarding tool(lar)ı YALNIZ bu persona'ya bağlanır. Shopper
  (`assistant`) ve anonim (`public`) persona'larına onboarding tool'u EKLENMEZ.
- **FR-004**: `admin` persona'sının kendi yönlendirici talimatı (prompt) OLMALIDIR — onboarding
  odaklı; shopper (sepet/ürün/taksit) talimatı taşımaz.
- **FR-005**: Yalnız yönetimin eriştiği bir **metin (chat) ekranı** OLMALIDIR; bu ekran ve onu
  besleyen BFF proxy ucu **`admin` rolüyle** korunur (`User.IsInRole("admin")`, RBAC 030). Admin
  olmayan/anonim erişim reddedilir.
- **FR-006**: BFF proxy (ChatEndpoints deseni), admin ekranından gelen mesajı **`admin` agent'a**
  yönlendirmelidir (mevcut public/assistant seçimine admin kolu eklenir veya ayrı admin ucu).
- **FR-007**: Gateway Merchant.Agent A2A adresi (ve gerekli kontrat sabitleri) **Options pattern** ile
  strongly-typed okunur (magic-string `config[...]` YASAK; `DropShopGatewayOption`/`GatewayOption`
  house-style — BindConfiguration + ValidateOnStart, düz POCO enjekte).
- **FR-008**: Mevcut **yapısal MCP yolu** (`GatewayRegistrationClient` + `POST
  /gateway-onboarding/register`) DEĞİŞMEDEN korunur (coexist); bu feature onun yerini almaz.
- **FR-009**: Gateway'e giden onboarding çağrısı **makine kimliğiyle** (`ecommerce-onboarding`
  client_credentials) gider; admin kullanıcının kişisel token'ı gateway'e TAŞINMAZ.

### Key Entities

- **`admin` agent persona** (yeni): ChatAgent'ta onboarding odaklı üçüncü agent; tool seti = onboarding
  A2A tool(lar)ı; prompt = onboarding yönlendirici.
- **Onboarding A2A tool** (yeni): Merchant.Agent'a bağlanan tool (`PaymentAgentInstallmentTool`
  muadili); `register` + `registration_status` skill'leri.
- **Admin onboarding ekranı** (yeni): RBAC-korumalı Razor sayfası + metin kutusu; BFF proxy ile `admin`
  agent'a SSE.
- **Merchant.Agent A2A config** (yeni Options alanı): agent url + kontrat sabitleri (skill id'leri,
  named HttpClient).
- **GatewayRegistrationClient** (değişmez): yapısal MCP yolu, coexist.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, onboarding ekranına doğal dille yazarak (kod/URL elle girmeden başka bir şey
  yapmadan) bir başvuru açabilir ve durumunu sorabilir; ikisi de metin yanıtla döner.
- **SC-002**: Onboarding tool(lar)ı YALNIZ `admin` persona'sında görünür; shopper/anonim
  asistanlarda onboarding aracı YOKTUR (persona izolasyonu).
- **SC-003**: Onboarding ekranı/endpoint'i admin olmayan veya anonim erişimde reddedilir (403 /
  görünmez).
- **SC-004**: Gateway Merchant.Agent erişilemezken ekran açılır ve dostça "kullanılamıyor" der; boot
  çökmez (graceful-degrade).
- **SC-005**: Mevcut yapısal MCP onboarding (`POST /gateway-onboarding/register`) davranışı
  değişmeden çalışır (coexist).
- **SC-006**: Çözüm sıfır derleme hatasıyla derlenir; config Merchant.Agent A2A url'i Options POCO ile
  okunur (magic-string yok).

## Assumptions

- **Merchant.Agent hazır**: Gateway tarafında Merchant.Agent A2A host'u + `register`/
  `registration_status` skill'leri + agent-card mevcut (PaymentGateway 013/015). Bu feature yalnız
  ECommerce **istemci** tarafını ekler.
- **RBAC 030 admin rolü**: `admin` rolü + `User.IsInRole("admin")` mevcut (030); yeni rol modeli
  kurulmaz, var olan kullanılır.
- **Agent framework A2A deseni**: `A2ACardResolver` → `GetAIAgentAsync` → `AsAIFunction` deseni
  (`PaymentAgentInstallmentTool`) yeniden kullanılır; yeni A2A altyapısı icat edilmez.
- **Options pattern**: Config house-style (`OptionsExt` + BindConfiguration + ValidateOnStart, düz
  POCO enjekte) — `DropShopGatewayOption`/`IdentityServerSettings` referans.
- **Coexist**: Yapısal MCP yolu (024/013 E1) korunur; A2A yolu alternatif metin yüzeyidir.
- **Kapsam dışı**: Charge/ödeme (G5), MerchantKey teslimi akışı değişikliği, gerçek A2A mesaj
  şemasının genişletilmesi (mevcut skill'ler yeterli), gateway tarafı değişiklikleri.