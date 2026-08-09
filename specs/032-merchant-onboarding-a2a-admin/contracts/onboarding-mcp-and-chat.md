# Sözleşmeler: WebApp Onboarding MCP + Admin Chat

## 1. WebApp Onboarding MCP yüzeyi (yeni)

WebApp host'unda MCP server (`MapMcp` / `WithToolsFromAssembly`), **admin-korumalı**. ChatAgent `admin`
persona named MCP client ile tüketir (token forward). Tool'lar `GatewayRegistrationClient` sarar.

### tool: `submit_registration`
- **Girdi**: `descriptorUrl?: string` (verilmezse WebApp kendi `/.well-known/merchant-descriptor.json`
  origin'inden türetilir — `POST /gateway-onboarding/register` deseni).
- **Davranış**: `GatewayRegistrationClient.RegisterAsync` → iki-adım (submit → ChallengeRequired ise
  `IChallengeStore.Set` → submit) **WebApp process'inde**.
- **Çıktı**: `{ status, requestId?, message? }` (gateway `RegisterResult` yansıması; ör. `Pending`).

### tool: `registration_status`
- **Girdi**: `domain: string`.
- **Davranış**: `GatewayRegistrationClient.StatusAsync(domain)` → Merchant.Api `/mcp registration_status`.
- **Çıktı**: `{ status, requestId?, message }` (AwaitingDomainControl/Pending/Approved/Rejected + metin).

**Yetki**: yüzey admin ile korunur (rol-cookie; anonim/normal reddedilir). Gateway'e giden çağrı içeride
makine kimliğiyle (`ecommerce-onboarding` client_credentials) — admin token'ı gateway'e gitmez.

## 2. Admin BFF chat ucu (ChatEndpoints deseni)

Mevcut `/chat/stream` public/assistant seçimine **admin kolu** eklenir veya ayrı `/chat/admin/stream`.

- **Koruma**: yalnız `admin` rolü (cookie). Anonim/normal → reddedilir.
- **Seçim**: admin kullanıcı → `admin` persona (`/admin/v1/responses`), token forward.
- **Girdi/çıktı**: mevcut `ChatRequest { Message, PreviousResponseId }` + SSE pass-through (değişmez şema).

## 3. `admin` persona prompt sözleşmesi (`Prompts.AdminOnboardingInstructions`)

Yönlendirici talimat (public/assistant deseni). Kapsam:
- Kullanıcı bir alan adı/site kaydı isterse → `submit_registration` (descriptor linki/origin ile). İki-adım
  WebApp'te otomatik; sonucu (durum + varsa sıradaki adım) metinle bildir.
- Durum sorusu → `registration_status` (domain ile).
- Yalnız dönen alanları göster, **alan UYDURMA**; yanıt eksikse eksik olduğunu söyle.
- Alışveriş/sepet/ürün/taksit niyetleri **KAPSAM DIŞI** — bu persona yalnız onboarding.
- Tool yoksa/çağrı başarısızsa: "onboarding şu an kullanılamıyor" de; teknik hata ayrıntısı verme.

## 4. Config sözleşmesi (Options)

- `DropShopGatewayOption` (mevcut): `{ IdentityAddress, McpUrl, ClientId, ClientSecret }` — WebApp→gateway.
- WebApp onboarding MCP server url'i: ChatAgent'ın erişimi için (Aspire service discovery / config, diğer
  McpServers deseni). Magic-string yok; Options/house-style.

## Değişmeyen (coexist)

- `POST /gateway-onboarding/register` + `GatewayRegistrationClient.RegisterAsync` davranışı korunur.
- `/.well-known/merchant-descriptor.json` + `/.well-known/merchant-challenge/{token}` + `POST
  /gateway-onboarding/challenge` (dev) değişmez.
- Gateway tarafı (Merchant.Api MCP, Merchant.Agent) değişmez; Merchant.Agent bu akışta kullanılmaz.