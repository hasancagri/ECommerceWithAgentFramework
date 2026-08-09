# Phase 1 Data Model: Admin Metinle Onboarding

Bu feature **yeni kalıcı domain/entity içermez** (başvuru gateway'de kalıcılaşır; challenge geçici
WebApp in-memory). Aşağıda kalıcı veri yerine **bileşenler + akış** modeli.

## Bileşenler

| Bileşen | Konum | Rol |
|---------|-------|-----|
| `admin` agent persona | ChatAgent (`AddAIAgent("admin")`) | LLM router; admin metnini onboarding MCP tool'larına yönlendirir. Tool seti = WebApp onboarding MCP. |
| `Prompts.AdminOnboardingInstructions` | ChatAgent `ConstValues.cs` | Persona yönlendirici prompt; register/status niyet→tool eşlemesi, sonucu metinle döner, shopper niyeti kapsam dışı. |
| WebApp onboarding MCP yüzeyi | WebApp `GatewayOnboarding/OnboardingMcpTools.cs` | `submit_registration` + `registration_status` tool'ları; admin-korumalı; `GatewayRegistrationClient` sarar. |
| `GatewayRegistrationClient` | WebApp (mevcut) | `RegisterAsync` (iki-adım+challenge, değişmez) + **`StatusAsync(domain)`** (yeni). |
| `IChallengeStore` / `InMemoryChallengeStore` | WebApp (mevcut) | Challenge değeri geçici yayın (register iki-adımı için). |
| Admin onboarding ekranı | WebApp `Pages/Admin/Onboarding.cshtml` | RBAC-korumalı metin kutusu; BFF proxy'ye SSE. |
| BFF proxy admin kolu | WebApp `Chat/ChatEndpoints.cs` | admin rolünde `admin` persona'ya yönlendirir. |
| `DropShopGatewayOption` | WebApp (mevcut, 032-prep) | Gateway MCP/Identity bağlantısı (Options). |

## Akış (durum diyagramı)

```
Admin (admin rolü) → Onboarding ekranı (metin) → BFF /chat/... (admin kolu, rol-korumalı)
   → ChatAgent `admin` persona (LLM router)
        ├─ niyet "kaydet"  → WebApp MCP submit_registration
        │      → GatewayRegistrationClient.RegisterAsync
        │           → Merchant.Api /mcp submit_registration (1. çağrı)
        │           → ChallengeRequired ise IChallengeStore.Set + tekrar çağrı → Pending
        │      → sonuç (Pending/durum + Message) → metin yanıt
        └─ niyet "durum"   → WebApp MCP registration_status
               → GatewayRegistrationClient.StatusAsync(domain)
                    → Merchant.Api /mcp registration_status → durum + Message → metin yanıt
```

## Doğrulama / invariant'lar (davranışsal)

- Onboarding MCP tool'ları YALNIZ `admin` persona tool setinde (persona izolasyonu).
- Ekran + BFF proxy + WebApp MCP yüzeyi admin-korumalı; anonim/normal reddedilir.
- register iki-adım + challenge tek process'te (WebApp) tamamlanır.
- Gateway çağrısı makine kimliğiyle; admin kullanıcı token'ı gateway'e taşınmaz.
- Gateway erişilemez → tool eklenmez / dostça metin; boot çökmez.