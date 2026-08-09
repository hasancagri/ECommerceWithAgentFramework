# Phase 0 Research: Admin Metinle Onboarding (back-end MCP routing)

UI + agent-config + MCP-wrapper feature'ı; yeni domain/teknoloji yok. Kararlar, mevcut desenleri
(GatewayRegistrationClient, McpServers/McpClients, public/assistant persona, ChatEndpoints, OptionsExt,
RBAC 030) nasıl yeniden kullanacağımızı ve challenge-locality'yi nasıl çözeceğimizi netleştirir.

## D1 — Admin gating: rol-cookie (WebApp UI) + makine-scope (gateway)

**Decision**: Admin onboarding ekranı + BFF proxy admin kolu + WebApp onboarding MCP yüzeyi **admin
rolü** ile korunur (`User.IsInRole("admin")` — mevcut `_Layout` admin menüsü deseni). Gateway'e giden
asıl onboarding çağrısı **makine kimliğiyle** (`ecommerce-onboarding` client_credentials) gider.

**Rationale**: Anayasa İlke V "downstream API rol ile korunmaz, scope ile" der; ama bu **WebApp'in kendi
cookie-UI yüzeyi** (IdP-admin-UI istisnası mantığı — rol otoritesinin kendi ekranını koruması). WebApp
zaten admin menüsünü `IsInRole` ile gösteriyor → tutarlı. Gateway API'si kendi scope'unu (merchant.write)
zorlar; ECommerce tarafı gateway'e rol taşımaz. "Makine kimlikleri RBAC dışı" (V) → onboarding çağrısı
client_credentials.

**Alternatives**: WebApp MCP yüzeyine ayrı bir management-scope zorunlu kılmak — daha katı ama WebApp
cookie-UI'da scope claim'i taşınmıyor (Layout IsInRole kullanıyor); mevcut pratikle uyumsuz, reddedildi.

## D2 — Ayrı `admin` agent persona (tool-in-assistant değil)

**Decision**: ChatAgent'ta 3. persona `AddAIAgent("admin")` (public/assistant deseni); onboarding
tool'ları yalnız burada.

**Rationale**: ChatAgent agent'ları **boot'ta singleton** kuruluyor (tool seti sabit, per-user değil).
Runtime'da assistant'a admin-only tool ekleyip çıkaramazsın. Ayrı persona = temiz izolasyon; shopper
prompt'u ve araçları onboarding'le karışmaz.

**Alternatives**: Assistant'a tool + invoke-anında rol kontrolü — singleton mimariyle uyumsuz, tool
herkesin setinde görünür; reddedildi.

## D3 — Back-end MCP routing (Merchant.Agent A2A KULLANILMAZ)

**Decision**: Gateway'in kendi Merchant.Agent (A2A LLM router) kullanılmaz. Router = ChatAgent `admin`
persona. Gateway'e **MCP** ile konuşulur (Merchant.Api `/mcp submit_registration` + `registration_status`),
WebApp onboarding MCP yüzeyi üzerinden.

**Rationale**: Kullanıcı kararı — akış back-end'de MCP routing ile kontrol edilsin. Merchant.Agent'ın LLM
router'ı nondeterministik + iki-adım challenge'ı yönetemez (aşağıda D4). Kendi persona'mız + MCP tool'ları
deterministik ve challenge'ı yerelde çözer.

**Alternatives**: Merchant.Agent'a A2A istemci (agent-card + AsAIFunction) — challenge-locality sorunu
(D4) + gateway LLM nondeterminizmi; reddedildi.

## D4 — Challenge-locality: register tool WebApp'te koşar (challenge store orada)

**Decision**: `submit_registration` MCP tool'u **WebApp**'te barınır ve içeride mevcut
`GatewayRegistrationClient.RegisterAsync`'i çağırır. RegisterAsync iki-adımı (submit → `IChallengeStore.Set`
→ submit) zaten **WebApp process'inde** çözüyor. Böylece ChatAgent'a challenge state taşınmaz; ChatAgent
yalnız MCP client (router).

**Rationale**: Domain-control challenge, adayın (ECommerce/WebApp) kendi `/.well-known/merchant-challenge/{token}`
yolunda değeri yayınlamasını gerektirir; challenge store WebApp in-memory'sinde. Tool ayrı ChatAgent
process'inde koşarsa store'a yazamaz. MCP yüzeyini WebApp'te açmak locality'yi çözer.

**Alternatives**: (a) ChatAgent'a local publish tool + cross-process HTTP → karmaşık; (b) WebApp otomatik
challenge → yeni otomasyon. RegisterAsync zaten iki-adımı çözdüğünden onu MCP tool olarak sarmak en sade.

## D5 — registration_status: GatewayRegistrationClient.StatusAsync eklenir

**Decision**: `GatewayRegistrationClient`'e `StatusAsync(string domain)` eklenir — gateway'in
`registration_status` MCP tool'unu (mevcut `CreateMcpClientAsync` + `CallToolAsync` deseni) çağırır ve
durum + Message metnini döner. WebApp onboarding MCP `registration_status` tool'u bunu sarar.

**Rationale**: `RegisterAsync` deseni (MCP client + token) yeniden kullanılır; status read-only, challenge
yok → basit.

## D6 — Config: Options pattern (magic-string yok)

**Decision**: WebApp onboarding MCP url'i (ChatAgent'ın erişmesi için) + gateway bağlantısı
(`DropShopGatewayOption`, 032-prep) house-style Options ile okunur (`OptionsExt` + BindConfiguration +
ValidateOnStart, düz POCO). ChatAgent tarafında onboarding MCP server url'i mevcut server-url okuma
deseniyle (diğer McpServers gibi Aspire service discovery / config) alınır.

**Rationale**: 032-prep'te konan kural; tutarlılık.

## D7 — Graceful-degrade

**Decision**: WebApp onboarding MCP url'i yok/erişilemezse `admin` persona onboarding tool'suz açılır
(mevcut `PaymentAgentInstallmentTool` fail-open deseni); ekran açılır, "onboarding kullanılamıyor" der.

**Rationale**: Boot dayanıklılığı; mevcut A2A/MCP tool ekleme deseniyle tutarlı.

## Revizyon (implement, kullanıcı kararı)

- **D4 GEÇERSİZ:** domain-control challenge tümüyle kaldırıldı (DropShop'ta). Admin descriptor'ı
  (legalName/taxId/contactEmail) insan olarak inceleyip onaylar; makine challenge dansı fazlalık.
  `submit` descriptor doğrulanınca doğrudan Pending doğar → insan Approve/Reject.
- **Challenge-locality yok → WebApp'te MCP yok.** ChatAgent `admin` persona doğrudan DropShop
  Merchant.Api `/mcp`'yi toplar (submit_registration + registration_status), makine token'ıyla.
  WebApp yalnız admin ekran + BFF proxy. D5 (StatusAsync) da gereksiz — status gateway MCP tool'u.
- **Metin akışında MCP DOLAYLI:** iş elle `CallToolAsync` ile değil, agent'ın topladığı tool + prompt
  ile yapılır (CLAUDE.md kuralı). D1 (admin rol-cookie gating), D2 (ayrı admin persona), D3
  (Merchant.Agent kullanılmaz), D6 (Options), D7 (graceful-degrade) korunur.