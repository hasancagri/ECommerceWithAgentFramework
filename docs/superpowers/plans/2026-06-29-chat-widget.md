# Müşteri Hizmetleri Chat Widget — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WebApp'e, her sayfada görünen, cevapları streaming akan ve arka tarafta MCP tool'larıyla iş yapabilen bir müşteri hizmetleri chat widget'ı eklemek.

**Architecture:** Tarayıcıdaki widget, WebApp içindeki bir BFF proxy endpoint'ine (`POST /chat/stream`) gider. Token HttpOnly cookie'de olduğu için tarayıcı orchestrator'a doğrudan erişemez; proxy auth durumuna göre token + agent seçer ve AgentOrchestrator'ın OpenAI-uyumlu **Responses** ucuna streaming istek atıp SSE'yi tarayıcıya pass-through aktarır. Çok turlu geçmiş orchestrator'da `previous_response_id` zinciriyle (RAM, InMemoryConversationStorage) tutulur.

**Tech Stack:** ASP.NET Core minimal API (proxy), Razor Pages partial + vanilla JS/jQuery (widget), .NET Aspire service discovery, Microsoft.Agents.AI.Hosting.OpenAI (Responses API), OIDC cookie auth + mevcut TokenService.

## Global Constraints

- Hedef framework: `net10.0` (tüm projeler).
- WebApp'te SPA framework YOK — vanilla JS + mevcut jQuery/Bootstrap kullanılır.
- Token tarayıcıya asla sızdırılmaz; agent seçimi sunucuda yapılır (anonim kullanıcı `assistant`'a ulaşamaz).
- Servis adresleri Aspire service discovery adlarıdır (`http://agent-orchestrator` gibi); hardcode port yok.
- **Test/TDD bu planın dışında** — kullanıcı kararı: "çalıştıktan sonra testleri çıkarırız". Her görevin kapısı `dotnet build` (0 error). Çalıştırarak (runtime/e2e) doğrulama **internet + Aspire** gerektirir ve şu an BLOKE; Task 5'te kontrol listesi olarak toplanır.
- Sohbet geçmişi orchestrator'da RAM'de; kalıcılık bilinçli ertelendi (bkz. spec "Açık konular").

**Build komutları (repo kökünden, mutlak yol ile — cd kaymasına karşı):**
- WebApp: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/ui/WebApp/WebApp.csproj`
- Orchestrator: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`
- AppHost: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AppHost/AppHost.csproj`

---

## File Structure

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/AppHost/AppHost.cs` | Aspire topolojisi: web → orchestrator referansı | Modify |
| `src/AgentOrchestrator/Program.cs` | Responses route'larını sabit path'e pinle (proxy için deterministik sözleşme) | Modify |
| `src/ui/WebApp/Chat/ChatEndpoints.cs` | `/chat/stream` BFF proxy (agent+token seçimi, SSE pass-through) | Create |
| `src/ui/WebApp/Program.cs` | "orchestrator" HttpClient kaydı + `MapChatProxy()` | Modify |
| `src/ui/WebApp/Pages/Shared/_ChatWidget.cshtml` | Widget markup (balon + panel + auth-state data attr) | Create |
| `src/ui/WebApp/wwwroot/css/chat-widget.css` | Widget stilleri | Create |
| `src/ui/WebApp/wwwroot/js/chat-widget.js` | Aç/kapa, gönder, SSE parse, previousResponseId yaşam döngüsü | Create |
| `src/ui/WebApp/Pages/Shared/_Layout.cshtml` | partial + css/js dahil etme | Modify |

---

## Task 1: Aspire — web'i orchestrator'a bağla

**Files:**
- Modify: `src/AppHost/AppHost.cs` (orchestrator kaydı ~101-103, web kaydı ~87-96)

**Interfaces:**
- Produces: WebApp resource'una `services__agent-orchestrator__http__0` env var'ı enjekte edilir → WebApp `http://agent-orchestrator` adını çözebilir.

- [ ] **Step 1: Orchestrator'ı bir değişkene al ve web'den referansla**

`AppHost.cs` sonundaki orchestrator kaydını değişkene atayıp, `web` zaten tanımlı olduğu için ona referans ekle (web → orchestrator; cycle yok: web→orchestrator→gateway→servisler):

```csharp
// AgentOrchestrator: MCP tool'lari uzerinden calisan AI agent API'si (OpenAI uyumlu endpoint'ler).
// MCP server'lara Gateway uzerinden baglanir; gateway'i WithReference ile alir.
var agentOrchestrator = builder.AddProject<Projects.AgentOrchestrator>("agent-orchestrator")
    .WithReference(gateway)
    .WaitFor(gateway);

// WebApp chat widget'i orchestrator'a proxy uzerinden gider => adres cozumu icin referans.
web.WithReference(agentOrchestrator);
```

(Not: `web` değişkeni dosyada ~87. satırda zaten var; bu blok orchestrator tanımının hemen altına, `await builder.Build().RunAsync();`'ten önce gelir.)

- [ ] **Step 2: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AppHost/AppHost.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 3: Commit**

```bash
git add src/AppHost/AppHost.cs
git commit -m "feat(apphost): wire webapp to agent-orchestrator for chat proxy" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Orchestrator — Responses route'larını sabitle

**Files:**
- Modify: `src/AgentOrchestrator/Program.cs:57-65`

**Interfaces:**
- Produces: İki sabit endpoint — `POST /public/v1/responses` (publicAgent) ve `POST /assistant/v1/responses` (assistant). Proxy (Task 3) tam bu path'leri çağırır. Conversations default path `/v1/conversations`'ta kalır.

**Neden:** Route default şablonu runtime'da doğrulanamıyor (internet bloklu). `path` overload'u ile path'i pinleyerek proxy↔orchestrator sözleşmesini deterministik yapıyoruz.

- [ ] **Step 1: Responses map çağrılarını explicit path overload'una çevir**

Mevcut (57-65 civarı):

```csharp
// Anonim kullanıcı agent'ı: POST /public/v1/chat/completions, /public/v1/responses
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent);

// Giriş yapmış kullanıcı agent'ı: POST /assistant/v1/chat/completions, /assistant/v1/responses
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant);

app.MapOpenAIConversations(); // POST /v1/conversations
```

Şununla değiştir (Responses path'leri sabitlenir; chat completions olduğu gibi kalır):

```csharp
// Anonim kullanıcı agent'ı.
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent, "/public/v1/responses");

// Giriş yapmış kullanıcı agent'ı.
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant, "/assistant/v1/responses");

app.MapOpenAIConversations(); // POST /v1/conversations
```

- [ ] **Step 2: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`
Expected: Build succeeded, 0 Error. (Overload imzası: `MapOpenAIResponses(this IEndpointRouteBuilder, IHostedAgentBuilder, string)` — XML dokümanında mevcut.)

- [ ] **Step 3: Commit**

```bash
git add src/AgentOrchestrator/Program.cs
git commit -m "feat(orchestrator): pin Responses route paths for proxy contract" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: WebApp — BFF proxy endpoint

**Files:**
- Create: `src/ui/WebApp/Chat/ChatEndpoints.cs`
- Modify: `src/ui/WebApp/Program.cs` (HttpClient kaydı ~37 civarı, endpoint map ~193 civarı)

**Interfaces:**
- Consumes: `TokenService.GetClientAccessTokenAsync()` (mevcut, `Duende.IdentityModel.Client.TokenResponse` döner; `.AccessToken`), `HttpContext.GetTokenAsync("access_token")` (mevcut OIDC). Orchestrator sabit path'leri (Task 2). "orchestrator" adlı HttpClient.
- Produces: `POST /chat/stream` endpoint'i. İstek gövdesi `{ "message": string, "previousResponseId": string|null }`. Yanıt: `text/event-stream` (orchestrator SSE'si aynen). `MapChatProxy(this IEndpointRouteBuilder)` extension.

- [ ] **Step 1: ChatEndpoints.cs oluştur**

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WebApp.Authentication;

namespace WebApp.Chat;

// Tarayicidaki chat widget'i ile AgentOrchestrator arasindaki BFF proxy.
// Token HttpOnly cookie'de oldugundan tarayici orchestrator'a dogrudan erisemez;
// burada auth durumuna gore agent + token secilir, SSE pass-through edilir.
public static class ChatEndpoints
{
    public sealed record ChatRequest(string Message, string? PreviousResponseId);

    public static IEndpointRouteBuilder MapChatProxy(this IEndpointRouteBuilder app)
    {
        app.MapPost("/chat/stream", async (
            ChatRequest body,
            HttpContext http,
            IHttpClientFactory httpClientFactory,
            TokenService tokenService,
            CancellationToken ct) =>
        {
            var isAuthenticated = http.User.Identity?.IsAuthenticated == true;

            // Auth durumuna gore agent ve token. Anonim 'assistant'a ULASAMAZ.
            var (agentPath, agentName) = isAuthenticated
                ? ("/assistant/v1/responses", "assistant")
                : ("/public/v1/responses", "public");

            var token = isAuthenticated
                ? await http.GetTokenAsync(OpenIdConnectParameterNames.AccessToken)
                  ?? throw new UnauthorizedAccessException("Access token bulunamadi.")
                : (await tokenService.GetClientAccessTokenAsync()).AccessToken!;

            // OpenAI Responses govdesi. Cok turlu gecmis previous_response_id ile zincirlenir
            // (orchestrator tarafinda RAM'de tutulur).
            var payload = new Dictionary<string, object?>
            {
                ["model"] = agentName,
                ["input"] = body.Message,
                ["stream"] = true,
            };
            if (!string.IsNullOrWhiteSpace(body.PreviousResponseId))
                payload["previous_response_id"] = body.PreviousResponseId;

            var client = httpClientFactory.CreateClient("orchestrator");
            using var request = new HttpRequestMessage(HttpMethod.Post, agentPath)
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var upstream = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            http.Response.StatusCode = (int)upstream.StatusCode;
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            await using var upstreamStream = await upstream.Content.ReadAsStreamAsync(ct);
            await upstreamStream.CopyToAsync(http.Response.Body, ct);
        }).AllowAnonymous();

        return app;
    }
}
```

- [ ] **Step 2: Program.cs — "orchestrator" HttpClient'ı kaydet**

`src/ui/WebApp/Program.cs` içinde, diğer HttpClient kayıtlarının yanına (ör. `builder.Services.AddHttpClient("identity");` satırının hemen altı, ~37):

```csharp
// AI chat orchestrator (service discovery: services:agent-orchestrator:http:0).
// Streaming oldugu icin uzun timeout.
builder.Services.AddHttpClient("orchestrator", client =>
{
    client.BaseAddress = new Uri("http://agent-orchestrator");
    client.Timeout = TimeSpan.FromMinutes(5);
});
```

- [ ] **Step 3: Program.cs — endpoint'i map et ve using ekle**

Dosyanın en üstündeki using'lere ekle:

```csharp
using WebApp.Chat;
```

`app.MapRazorPages().WithStaticAssets();` satırının hemen altına (auth middleware'lerden sonra, ~194):

```csharp
app.MapChatProxy();
```

- [ ] **Step 4: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/ui/WebApp/WebApp.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 5: Commit**

```bash
git add src/ui/WebApp/Chat/ChatEndpoints.cs src/ui/WebApp/Program.cs
git commit -m "feat(webapp): add chat BFF proxy to agent-orchestrator" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: WebApp — chat widget (frontend)

**Files:**
- Create: `src/ui/WebApp/Pages/Shared/_ChatWidget.cshtml`
- Create: `src/ui/WebApp/wwwroot/css/chat-widget.css`
- Create: `src/ui/WebApp/wwwroot/js/chat-widget.js`
- Modify: `src/ui/WebApp/Pages/Shared/_Layout.cshtml` (css head'e, partial body sonuna, js script'lerin yanına)

**Interfaces:**
- Consumes: `POST /chat/stream` (Task 3). Auth state, partial'da `data-authenticated` attribute'u ile DOM'a aktarılır (JS, oturum değişiminde geçmişi resetlemek için kullanır).
- Produces: Tüm sayfalarda sağ-altta widget.

- [ ] **Step 1: _ChatWidget.cshtml partial oluştur**

```html
@using System.Security.Claims
@{
    var isAuthenticated = User.Identity?.IsAuthenticated == true;
}
<div id="chat-widget" data-authenticated="@(isAuthenticated ? "true" : "false")">
    <button id="chat-toggle" type="button" aria-label="Yardim">💬</button>
    <div id="chat-panel" class="chat-hidden">
        <div id="chat-header">
            <span>Yardimci</span>
            <button id="chat-close" type="button" aria-label="Kapat">×</button>
        </div>
        <div id="chat-messages"></div>
        <form id="chat-form">
            <input id="chat-input" type="text" autocomplete="off"
                   placeholder="Bir sey sorun..." />
            <button type="submit">Gonder</button>
        </form>
    </div>
</div>
```

- [ ] **Step 2: chat-widget.css oluştur**

```css
#chat-widget { position: fixed; right: 20px; bottom: 20px; z-index: 1050; font-size: 14px; }
#chat-toggle {
    width: 56px; height: 56px; border-radius: 50%; border: none;
    background: #0d6efd; color: #fff; font-size: 24px; cursor: pointer;
    box-shadow: 0 4px 12px rgba(0,0,0,.2);
}
#chat-panel {
    position: absolute; right: 0; bottom: 70px; width: 340px; height: 460px;
    background: #fff; border: 1px solid #dee2e6; border-radius: 8px;
    box-shadow: 0 8px 24px rgba(0,0,0,.18); display: flex; flex-direction: column;
}
#chat-panel.chat-hidden { display: none; }
#chat-header {
    display: flex; justify-content: space-between; align-items: center;
    padding: 10px 12px; background: #0d6efd; color: #fff;
    border-top-left-radius: 8px; border-top-right-radius: 8px;
}
#chat-header button { background: none; border: none; color: #fff; font-size: 20px; cursor: pointer; }
#chat-messages { flex: 1; overflow-y: auto; padding: 12px; display: flex; flex-direction: column; gap: 8px; }
.chat-msg { padding: 8px 10px; border-radius: 10px; max-width: 80%; white-space: pre-wrap; word-wrap: break-word; }
.chat-msg.user { align-self: flex-end; background: #0d6efd; color: #fff; }
.chat-msg.bot { align-self: flex-start; background: #f1f3f5; color: #212529; }
#chat-form { display: flex; border-top: 1px solid #dee2e6; padding: 8px; gap: 6px; }
#chat-input { flex: 1; border: 1px solid #ced4da; border-radius: 6px; padding: 6px 8px; }
#chat-form button { border: none; background: #0d6efd; color: #fff; border-radius: 6px; padding: 6px 12px; cursor: pointer; }
```

- [ ] **Step 3: chat-widget.js oluştur**

SSE'yi `fetch` + `ReadableStream` ile okur, satır satır ayrıştırır. Responses event şekli runtime'da doğrulanacak (Task 5); parser savunmacı: `data:` JSON'unda `delta` string ise metne ekler, `response.id`/`id` görürse previousResponseId olarak saklar.

```javascript
(function () {
    const root = document.getElementById('chat-widget');
    if (!root) return;

    const toggle = document.getElementById('chat-toggle');
    const panel = document.getElementById('chat-panel');
    const closeBtn = document.getElementById('chat-close');
    const form = document.getElementById('chat-form');
    const input = document.getElementById('chat-input');
    const messages = document.getElementById('chat-messages');

    const PREV_KEY = 'chat.previousResponseId';
    const AUTH_KEY = 'chat.authState';

    // Oturum durumu degistiyse (login/logout) gecmisi resetle.
    const authState = root.getAttribute('data-authenticated');
    if (sessionStorage.getItem(AUTH_KEY) !== authState) {
        sessionStorage.removeItem(PREV_KEY);
        sessionStorage.setItem(AUTH_KEY, authState);
    }

    toggle.addEventListener('click', () => panel.classList.toggle('chat-hidden'));
    closeBtn.addEventListener('click', () => panel.classList.add('chat-hidden'));

    function addBubble(text, who) {
        const el = document.createElement('div');
        el.className = 'chat-msg ' + who;
        el.textContent = text;
        messages.appendChild(el);
        messages.scrollTop = messages.scrollHeight;
        return el;
    }

    function handleData(jsonText, botEl) {
        if (jsonText === '[DONE]') return;
        let obj;
        try { obj = JSON.parse(jsonText); } catch { return; }
        if (typeof obj.delta === 'string') { botEl.textContent += obj.delta; }
        else if (obj.output_text && typeof obj.output_text === 'string') { botEl.textContent += obj.output_text; }
        const id = (obj.response && obj.response.id) || obj.id;
        if (id && typeof id === 'string' && id.indexOf('resp') === 0) {
            sessionStorage.setItem(PREV_KEY, id);
        }
        messages.scrollTop = messages.scrollHeight;
    }

    async function send(message) {
        addBubble(message, 'user');
        const botEl = addBubble('', 'bot');
        const previousResponseId = sessionStorage.getItem(PREV_KEY);

        let resp;
        try {
            resp = await fetch('/chat/stream', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: message, previousResponseId: previousResponseId })
            });
        } catch {
            botEl.textContent = 'Su an yardimci olamiyorum.';
            return;
        }
        if (!resp.ok || !resp.body) { botEl.textContent = 'Su an yardimci olamiyorum.'; return; }

        const reader = resp.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });
            const parts = buffer.split('\n\n');      // SSE event ayraci
            buffer = parts.pop();                     // tamamlanmamis son parca
            for (const part of parts) {
                for (const line of part.split('\n')) {
                    const trimmed = line.trim();
                    if (trimmed.indexOf('data:') === 0) {
                        handleData(trimmed.slice(5).trim(), botEl);
                    }
                }
            }
        }
        if (botEl.textContent === '') botEl.textContent = '(bos yanit)';
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        const message = input.value.trim();
        if (!message) return;
        input.value = '';
        send(message);
    });
})();
```

- [ ] **Step 4: _Layout.cshtml — css, partial ve js'i dahil et**

`<head>` içine, `site.css`'ten sonra:

```html
<link rel="stylesheet" href="~/css/chat-widget.css" asp-append-version="true"/>
```

`</footer>` ile `<script src="~/lib/jquery...">` arasına (body içinde, RenderBody dışında — tüm sayfalarda çıksın):

```html
<partial name="_ChatWidget"/>
```

`site.js` script satırından sonra:

```html
<script src="~/js/chat-widget.js" asp-append-version="true"></script>
```

- [ ] **Step 5: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/ui/WebApp/WebApp.csproj`
Expected: Build succeeded, 0 Error. (cshtml/wwwroot dosyaları build'i bozmamalı; partial adı `_ChatWidget` doğru çözülmeli.)

- [ ] **Step 6: Commit**

```bash
git add src/ui/WebApp/Pages/Shared/_ChatWidget.cshtml src/ui/WebApp/wwwroot/css/chat-widget.css src/ui/WebApp/wwwroot/js/chat-widget.js src/ui/WebApp/Pages/Shared/_Layout.cshtml
git commit -m "feat(webapp): add customer-service chat widget UI" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Runtime doğrulama kontrol listesi (BLOKE — internet + Aspire gerekir)

**Files:** yok (manuel doğrulama). Bu görev kod üretmez; uygulama çalıştırılabildiğinde yapılacak doğrulamaları ve olası düzeltme noktalarını sabitler.

> Kullanıcının interneti olmadığı ve orchestrator OpenAI'a çıkamadığı için bu adımlar şu an **çalıştırılamaz**. Internet + `dotnet run --project src/AppHost` mümkün olunca yürütülür.

- [ ] **Doğrulama 1 — Widget görünür:** Her sayfada sağ-altta balon çıkıyor; aç/kapa çalışıyor.
- [ ] **Doğrulama 2 — Anonim akış:** Login olmadan mesaj at → `/public` agent → katalog sorusu cevaplanıyor; sepet işlemi istenirse "giriş yap" yönlendirmesi geliyor.
- [ ] **Doğrulama 3 — Login akış:** Giriş yap → mesaj at → `/assistant` agent → "şunu sepete ekle" tool çağrısını tetikliyor (auth düğümü çözülmüşse; bkz. spec açık konu).
- [ ] **Doğrulama 4 — Streaming:** Cevap token token akıyor (tek blok değil).
- [ ] **Doğrulama 5 — SSE event şekli:** Tarayıcı Network sekmesinde `/chat/stream` yanıtındaki gerçek `data:` event'lerini incele. `chat-widget.js > handleData` içindeki alan adlarını (`delta`, `response.id`) gerçek Responses event şemasına göre düzelt (delta event tipi farklıysa).
- [ ] **Doğrulama 6 — Çok turlu hafıza:** İkinci mesajda önceki bağlam hatırlanıyor (previousResponseId zinciri çalışıyor); sayfa yenilense de sohbet sürüyor; login/logout'ta sıfırlanıyor.
- [ ] **Doğrulama 7 — Route sözleşmesi:** `/public/v1/responses` ve `/assistant/v1/responses` 404 vermiyor (Task 2 pin'i tuttu).

---

## Self-Review Notu (yazım anında)

- **Spec kapsamı:** Widget (Task 4), proxy + agent/token seçimi (Task 3), orchestrator-side history (Task 2 pin + Task 3 previous_response_id), Aspire wiring (Task 1), streaming (Task 3 pass-through + Task 4 parse) — hepsi karşılandı. Auth düğümü spec'te bilinçli ertelendi; bu plan UI'yı ondan bağımsız kurar (Doğrulama 3 not düşülü).
- **Tip tutarlılığı:** `ChatRequest(Message, PreviousResponseId)` ↔ JS gövdesi `{ message, previousResponseId }` (camelCase, minimal API default JSON ile eşleşir). `MapChatProxy` adı Program.cs ve ChatEndpoints.cs'te aynı.
- **Runtime belirsizlikleri** (çalıştırma bloklu): (a) Responses SSE event alan adları → Doğrulama 5'te düzeltilecek; (b) `path` overload'unun tam path semantiği → Doğrulama 7. İkisi de izole ve işaretli; kod savunmacı yazıldı.