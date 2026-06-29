# Müşteri Hizmetleri Chat Widget — Tasarım

Tarih: 2026-06-29
Durum: Onaylandı (UI-first; auth ayrı iş olarak ertelendi)

## Amaç

WebApp'e, kullanıcıların doğal dille konuşabildiği bir "müşteri hizmetleri"
sohbet widget'ı eklemek. Agent yalnızca soru cevaplamaz; konuşma sırasında
MCP tool'ları üzerinden arka tarafta gerçek işlem de yapabilir (ör. sepete
ekleme). Cevaplar canlı (streaming) akar.

## Kapsam

- Widget **herkese** görünür (anonim + login).
- Anonim kullanıcı → `public` agent (yalnızca catalog, "giriş yap" yönlendirir).
- Login kullanıcı → `assistant` agent (catalog + basket).
- Sohbet geçmişi **orchestrator'da, RAM'de** tutulur (Conversations API).
  DB kalıcılığı bilinçli olarak ertelendi — bkz. Açık Konular.

## Mimari

```
Tarayıcı (chat JS, geçmiş = conversationId @ sessionStorage)
   │  POST /chat/stream   (cookie ile kimlikli; anonim de olabilir)
   ▼
WebApp ── BFF proxy endpoint ──┐
   │  - login  → kullanıcı access_token (cookie'den)  → assistant agent
   │  - anonim → M2M client_credentials token         → public agent
   │  - orchestrator SSE çıktısını tarayıcıya pass-through aktarır
   ▼
AgentOrchestrator  (/assistant | /public, Responses API + Conversations)
   │  geçmişi conversationId ile RAM'de tutar
   │  gelen token'ı her istekte MCP'ye forward eder (mevcut yapı)
   ▼
Gateway → MCP (catalog / basket)
```

**Neden BFF proxy zorunlu:** Kullanıcı access_token'ı HttpOnly cookie'de
(OIDC `SaveTokens=true`). Tarayıcı JS token'a erişemez → orchestrator'a
doğrudan istek atamaz. Araya WebApp içinde proxy gerekiyor. Bu, mevcut
`AuthenticatedHttpClientHandler` mantığının aynısıdır, hedefi orchestrator.

## Bileşenler

### 1. Frontend (widget)
- `Pages/Shared/_ChatWidget.cshtml` partial — `_Layout.cshtml`'e eklenir,
  tüm sayfalarda sağ-altta açılır/kapanır balon (kapalıyken sadece ikon).
- Mesaj listesi + input. Vanilla JS (jQuery mevcut) + `fetch` ile SSE okuma.
  SPA framework yok.
- `conversationId` ve mesaj balonları `sessionStorage`'da (sayfa gezintisinde
  sohbet korunur). Küçük ekran responsive derdine girilmez (bilinçli karar).
- Login/logout geçişinde **yeni conversation** başlatılır (agent ve yetki
  değişir; public↔assistant arası geçmiş taşınmaz).

### 2. Backend (WebApp proxy)
- `Program.cs`'e `app.MapPost("/chat/stream", ...)` minimal API endpoint
  (Razor handler yerine SSE pass-through için daha temiz). `AllowAnonymous`.
- Login durumu `HttpContext.User.Identity.IsAuthenticated` ile okunur.
- Token mevcut altyapıdan alınır: login → `GetTokenAsync(access_token)`,
  anonim → `TokenService` M2M client_credentials.
- Orchestrator adresi `services:agent-orchestrator:http:0`'tan çözülür
  (AppHost referansı ile). İstek `assistant` veya `public` uca gider.
- Orchestrator SSE çıktısı satır satır tarayıcıya yazılır.

### 3. Aspire wiring
- `AppHost.cs`: `web.WithReference(agentOrchestrator)` eklenir; böylece WebApp
  `services:agent-orchestrator:http:0`'ı çözer. Yön: web → orchestrator.

## Veri akışı (örnek: "şu mavi tişörtü sepete ekle")

1. Tarayıcı POST /chat/stream { message, conversationId } (cookie ile).
2. Proxy login tespiti → access_token → assistant uca SSE isteği.
3. Orchestrator geçmişi conversationId ile yükler, LLM `add_to_cart` tool'unu
   çağırır → Gateway/basket MCP → Basket DB güncellenir.
4. LLM cevabı ("ekledim") token token SSE ile geri akar; proxy tarayıcıya
   aktarır; tarayıcı balona yazar, conversationId'yi saklar.

## Hata yönetimi
- Orchestrator/upstream hata → widget'ta kullanıcı dostu mesaj ("şu an
  yardımcı olamıyorum"), teknik detay loglanır.
- Anonim kullanıcıda sepet işlemi denenirse → public agent zaten tool'a sahip
  değil; "giriş yap" yönlendirir (prompt davranışı).
- 401/token süresi: proxy mevcut refresh mantığına yaslanır (login akışı).

## Test
- Proxy endpoint: login/anonim ayrımı doğru agent ucunu mu seçiyor.
- SSE pass-through: parça parça gelen yanıt tarayıcıya bozulmadan ulaşıyor mu.
- Widget JS: conversationId yaşam döngüsü (yeni sohbet, login geçişinde reset).
- Manuel/uçtan uca doğrulama internet + Aspire çalıştırma gerektirir (şu an
  kısıtlı) — bu durum planda not edilecek.

## Açık konular / ertelenen işler
- **Auth policy düğümü (ayrı iş):** Gateway'de catalog-mcp-route=ClientCredential,
  basket-mcp-route=Password. Orchestrator MCP'lere tek Authorization forward
  ediyor. Login kullanıcıda `assistant` her ikisini de çağırır; tek token iki
  policy'yi aynı anda karşılamayabilir. Uçtan uca çalışmayı etkiler ama UI'dan
  bağımsız çözülür. Bkz. agent-auth-model notu. **Bu tasarım UI'yı bundan
  bağımsız kurar.**
- Conversation geçmişi RAM'de: restart'ta kaybolur, >1 instance'ta sticky/
  shared store gerekir, eviction/timeout yok (sınırsız büyüme borcu). DB
  kalıcılığı sonraya bırakıldı.