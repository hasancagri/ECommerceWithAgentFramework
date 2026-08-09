# Feature Specification: Admin — Metinle Merchant Onboarding (back-end MCP routing)

**Feature Branch**: `032-merchant-onboarding-a2a-admin`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "Yalnız yönetimin (admin) kullanacağı bir metin ekranından, doğal dille merchant onboarding başvurusu (register) + durum sorgusu (registration_status) yapılabilsin. Gateway ile konuşuruz ama akışı ECommerce back-end MCP routing ile kontrol eder: WebApp bir onboarding MCP yüzeyi açar (register iki-adımı + challenge yayınını kendi process'inde çözer), ChatAgent'a yeni bir `admin` agent persona'sı + kendi prompt'u eklenir ve bu MCP'ye yönlendirir. Ekran RBAC ile admin-korumalı. Mevcut GatewayRegistrationClient yapısal yolu korunur (coexist). Yeni proje YOK."

## Problem

Bugün ECommerce, DropShop gateway'e onboarding başvurusunu yalnız **yapısal MCP** ile, tek tetikle
(`POST /gateway-onboarding/register` → `GatewayRegistrationClient`) yapıyor; **metin/sohbet** girişi
yok. Yönetim, doğal dille ("shop.example.com'u gateway'e kaydet", "başvurum ne durumda?") onboarding
yürütmek istiyor.

Bu feature, ECommerce'e **admin-only bir metin (chat) yüzeyi** ekler. Akış **back-end MCP routing** ile
kontrol edilir: WebApp bir **onboarding MCP yüzeyi** açar (register'ın iki-adımlı domain-control
challenge'ını **kendi process'inde** — mevcut `GatewayRegistrationClient` + challenge store — çözer);
ChatAgent'a yeni bir **`admin` agent persona'sı** (public/assistant gibi 3.) + kendi yönlendirici
prompt'u eklenir ve admin'in metnini bu MCP tool'larına (`submit_registration` / `registration_status`)
yönlendirir. Gateway'in kendi Merchant.Agent (A2A LLM router) **kullanılmaz** — router bizde.

## Clarifications

### Session 2026-08-09

- Q: A2A yolu mevcut yapısal MCP kaydını değiştirsin mi? → A: Coexist. Metin yolu **eklenir**;
  `GatewayRegistrationClient` + `POST /gateway-onboarding/register` otomasyon için kalır.
- Q: Giriş noktası nerede? → A: Ayrı **`admin` agent persona** (public/assistant gibi 3.; ChatAgent
  agent'ları boot'ta singleton → per-user tool eklenemez, persona ayrımı doğru) + yalnız yönetimin
  eriştiği korumalı metin ekranı. Onboarding tool'ları yalnız bu persona'da.
- Q: Hangi skill'ler? → A: `register` (submit_registration) + `registration_status`.
- Q: Gateway'in Merchant.Agent A2A'sı mı, yoksa back-end MCP routing mi? → A: **Back-end MCP routing.**
  Merchant.Agent (gateway A2A LLM router) KULLANILMAZ; router ChatAgent `admin` persona'sıdır. Gateway'e
  MCP ile konuşulur.
- Q: register iki-adım challenge (WebApp-local) nasıl çözülür? → A: **WebApp bir onboarding MCP yüzeyi
  açar**; `submit_registration` tool'u içeride `GatewayRegistrationClient.RegisterAsync`'i sarar
  (iki-adım + challenge yayını aynı process'te, mevcut `IChallengeStore`). Böylece challenge-locality
  sorunu çözülür (tool WebApp'te çalışır). ChatAgent yalnız router; MCP'ye yönlendirir.
- Q: Admin-gating nerede? → A: WebApp ekranı/BFF proxy'si **admin rolü** ile (cookie-UI, mevcut
  `_Layout` `User.IsInRole("admin")` deseni); WebApp onboarding MCP yüzeyi admin ile korunur. Gateway'e
  giden asıl çağrı **makine kimliğiyle** (`ecommerce-onboarding` client_credentials, RBAC-dışı).
- Q: Yeni proje mi? → A: Hayır. `admin` persona → mevcut ChatAgent; MCP yüzeyi + ekran + proxy → mevcut
  WebApp. Sıfır yeni csproj.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin metinle başvuru yapar (Priority: P1)

Admin rolündeki yönetici, admin-korumalı onboarding ekranındaki metin kutusuna doğal dille başvuru
yazar ("shop.example.com'u gateway'e kaydet"). Mesaj `admin` persona'ya gider; persona LLM'i WebApp
onboarding MCP'sinin `submit_registration` tool'unu seçer; tool WebApp içinde `GatewayRegistrationClient`
ile iki-adımı (challenge dahil) çözer; sonuç (Pending / durum) metinle döner.

**Why this priority**: Feature'ın çekirdeği. Tek başına teslim edilebilir, değer üretir.

**Independent Test**: Admin ekranına "X sitesini kaydet" yaz → `admin` persona `submit_registration`
MCP tool'unu çağırır → WebApp iki-adımı çözer → başvuru Pending, yanıt metinle döner. Admin olmayan
ekrana/MCP'ye erişemez.

**Acceptance Scenarios**:

1. **Given** admin rolünde giriş yapmış kullanıcı, **When** ekrana doğal dille başvuru yazar, **Then**
   `admin` persona `submit_registration` MCP tool'unu çağırır, WebApp iki-adımlı challenge'ı yerelde
   çözer ve sonuç (Pending + varsa mesaj) metinle döner.
2. **Given** gateway erişilemez, **When** admin başvuru yazar, **Then** ekran çökmez; kullanıcıya durum
   nazikçe metinle bildirilir (graceful-degrade).
3. **Given** admin OLMAYAN/anonim kullanıcı, **When** onboarding ekranına/MCP yüzeyine erişmeye çalışır,
   **Then** reddedilir (ekran görünmez / MCP yüzeyi yetkisiz).

---

### User Story 2 - Admin başvuru durumunu sorar (Priority: P2)

Admin aynı ekrandan "shop.example.com başvurum ne durumda?" yazar; `admin` persona
`registration_status` MCP tool'unu seçer; WebApp gateway'in `registration_status`'ını çağırır; güncel
durum + sıradaki adım metinle döner.

**Why this priority**: Takip yeteneği; register'dan bağımsız test edilebilir.

**Independent Test**: Var olan domain için "durumu ne?" yaz → `registration_status` çağrılır → durum
metni döner.

**Acceptance Scenarios**:

1. **Given** açılmış başvuru, **When** admin domain ile durum sorar, **Then** `registration_status`
   tetiklenir ve güncel durum (AwaitingDomainControl/Pending/Approved/Rejected) + sıradaki adım metinle döner.
2. **Given** başvurusu olmayan domain, **When** durum sorar, **Then** "bulunamadı" benzeri açık metin döner.

---

### Edge Cases

- **Persona sızıntısı**: onboarding MCP tool'ları yalnız `admin` persona'nın tool setinde; shopper
  (`assistant`) / anonim (`public`) persona'larına sızmaz; `admin` persona'da shopper araçları yok.
- **Config eksik**: WebApp onboarding MCP url'i yoksa `admin` persona tool eklenmez (graceful-degrade);
  ekran açılır, "onboarding kullanılamıyor" der.
- **Yetki**: ekran + BFF proxy + WebApp MCP yüzeyi yalnız admin'e açık; anonim/normal kullanıcı erişemez.
- **Gateway auth**: WebApp→gateway çağrısı makine kimliğiyle; admin kullanıcı token'ı gateway'e taşınmaz.
- **Challenge locality**: register tool'u WebApp process'inde çalışır (challenge store orada) → iki-adım
  bir process içinde tamamlanır; ChatAgent'a challenge state taşınmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: WebApp, bir **onboarding MCP yüzeyi** SUNMALIDIR; iki tool: `submit_registration`
  (içeride `GatewayRegistrationClient.RegisterAsync` — iki-adım + challenge yayını aynı process) ve
  `registration_status` (gateway `registration_status`'ını çağırır). Yüzey **admin** ile korunur.
- **FR-002**: `submit_registration` MCP tool'u iki-adımlı domain-control challenge'ı **WebApp içinde**
  (`IChallengeStore` + `GatewayRegistrationClient`) çözmelidir; challenge state ChatAgent'a taşınmaz.
- **FR-003**: ChatAgent'ta **`admin`** adında 3. agent persona'sı (`AddAIAgent("admin")`, public/assistant
  deseni) OLMALIDIR; tool seti = WebApp onboarding MCP (`submit_registration` + `registration_status`).
  Onboarding tool'ları shopper/anonim persona'ya EKLENMEZ.
- **FR-004**: `admin` persona'sının **kendi yönlendirici prompt'u** OLMALIDIR (ör.
  `Prompts.AdminOnboardingInstructions`) — onboarding odaklı; shopper (sepet/ürün/taksit) talimatı taşımaz;
  register/status niyetini ilgili tool'a yönlendirir, sonucu metinle döner, alan uydurmaz.
- **FR-005**: Yalnız yönetimin eriştiği bir **metin (chat) ekranı** OLMALIDIR; ekran ve BFF proxy ucu
  **admin rolüyle** korunur (`User.IsInRole("admin")`, mevcut cookie-UI deseni). Admin olmayan/anonim erişim reddedilir.
- **FR-006**: BFF proxy (ChatEndpoints deseni), admin ekranından geleni **`admin` persona'ya**
  yönlendirmelidir (public/assistant seçimine admin kolu, veya ayrı admin ucu).
- **FR-007**: WebApp onboarding MCP url'i (ChatAgent'ın erişmesi için) ve DropShop gateway bağlantısı
  **Options pattern** ile strongly-typed okunur (magic-string `config[...]` YASAK; house-style
  `OptionsExt` + `BindConfiguration` + `ValidateOnStart`, düz POCO enjekte). `DropShopGatewayOption`
  (mevcut, 032-prep) yeniden kullanılır.
- **FR-008**: Mevcut yapısal yol (`GatewayRegistrationClient` + `POST /gateway-onboarding/register`)
  DEĞİŞMEDEN korunur (coexist); MCP tool onu SARAR, kaldırmaz.
- **FR-009**: WebApp→gateway onboarding çağrısı **makine kimliğiyle** (`ecommerce-onboarding`
  client_credentials) gider; admin kullanıcının kişisel token'ı gateway'e TAŞINMAZ.

### Key Entities

- **`admin` agent persona** (yeni, ChatAgent): onboarding odaklı 3. agent; tool = WebApp onboarding MCP;
  prompt = `AdminOnboardingInstructions`.
- **WebApp onboarding MCP yüzeyi** (yeni, WebApp): `submit_registration` (GatewayRegistrationClient sarar)
  + `registration_status` tool'ları; admin-korumalı.
- **Admin onboarding ekranı** (yeni, WebApp): RBAC-korumalı Razor sayfası + metin kutusu; BFF proxy ile
  `admin` persona'ya SSE.
- **GatewayRegistrationClient** (mevcut, WebApp): iki-adım register (challenge dahil); MCP tool tarafından
  sarılır; `POST /gateway-onboarding/register` coexist. (registration_status için yeni bir çağrı metodu eklenir.)
- **Config Options** (mevcut/genişler): `DropShopGatewayOption` + WebApp MCP url'i.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, ekrana doğal dille yazarak bir başvuru açabilir ve durumunu sorabilir; ikisi de metin yanıtla döner.
- **SC-002**: Onboarding MCP tool'ları YALNIZ `admin` persona'da görünür; shopper/anonim'de yoktur.
- **SC-003**: Ekran/BFF proxy/MCP yüzeyi admin olmayan veya anonim erişimde reddedilir.
- **SC-004**: Gateway erişilemezken ekran açılır ve dostça "kullanılamıyor" der; boot çökmez.
- **SC-005**: Mevcut yapısal onboarding (`POST /gateway-onboarding/register`) davranışı değişmeden çalışır (coexist).
- **SC-006**: Çözüm sıfır derleme hatasıyla derlenir; WebApp MCP url + gateway bağlantısı Options POCO ile okunur (magic-string yok).

## Assumptions

- **Gateway hazır**: Merchant.Api `/mcp` `submit_registration` + `registration_status` mevcut (PaymentGateway
  013/015). Merchant.Agent (gateway A2A LLM router) BU FEATURE'DA KULLANILMAZ.
- **RBAC admin rolü**: `admin` rolü + `User.IsInRole("admin")` mevcut (030); yeni rol kurulmaz.
- **MCP altyapısı**: WebApp'te MCP server barındırma + ChatAgent'ta named MCP client (`McpClients.WithToken`)
  deseni mevcut; yeniden kullanılır (yeni altyapı icat edilmez).
- **Options pattern**: house-style (`OptionsExt` + BindConfiguration + ValidateOnStart, düz POCO); 032-prep
  `DropShopGatewayOption` mevcut.
- **Coexist**: `GatewayRegistrationClient` + `POST /gateway-onboarding/register` korunur.
- **Kapsam dışı**: charge/ödeme (G5), MerchantKey teslim akışı, gateway tarafı değişiklikleri, yeni proje/csproj.