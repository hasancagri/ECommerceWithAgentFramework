# Orchestrator Request-Aware Agent — Design Spec

**Tarih:** 2026-06-29
**Durum:** Tasarım onayı bekliyor
**Bağlam:** Chat widget doğrulaması sırasında ortaya çıkan mimari düzeltme (Option C).

## 1. Amaç

Orchestrator agent'larının MCP tool'larını **istek başına, doğru token'la** toplamasını sağlamak. Böylece:
- **Anonim** kullanıcı → `public` agent → uygulamanın kendi (client_credentials / `m2m.client`) token'ıyla **catalog** tool'ları.
- **Login** kullanıcı → `assistant` agent → isteğindeki **kullanıcı token'ıyla** catalog + basket tool'ları (kullanıcı tüm scope'lara sahip).

## 2. Mevcut durum ve problem

Bugün agent'lar `Singleton` ve tool'ları **açılışta bir kez**, token olmadan topluyor (`ChatClientAgent` constructor'ı tool listesini kurulumda istiyor). Sonuç: her iki agent da boş tool ile çalışıyor.

Sebep — kütüphane kısıtı (decompile ile doğrulandı): `MapOpenAIChatCompletions`/`MapOpenAIResponses` agent'ı **açılışta root provider'dan tek sefer** çözüp closure'a yakalıyor. Dolayısıyla:
- `Scoped` agent çalışmaz (root'tan çözülemez → boot crash).
- `Singleton` agent çalışır ama tool'lar kurulum anında donar.

Kütüphanenin map handler'ı (`AIAgentResponseExecutor`, `CreateChatCompletionAsync(agent, ...)`) agent'ı **kendi içinde** çağırdığı için, çağrıya dışarıdan `options` geçemiyoruz. Tek enjeksiyon noktası **agent'ın kendi Run metodu**.

**Etkinleştirici bulgu:** `ChatClientAgentRunOptions.ChatOptions.Tools` çağrı anında verildiğinde agent'ın tool'larıyla **union'lanıyor** (decompile XML notu). Yani agent'ı tool'suz kurup, her çağrıda tool'ları run-options ile geçebiliriz.

## 3. Yaklaşım (A): Delegating request-aware agent

`DelegatingAIAgent` türeyen ince bir sarmalayıcı; `Singleton` kayıtlı (kütüphane kısıtını karşılar). İçinde **tool'suz** bir `ChatClientAgent` (`InnerAgent`) tutar. `DelegatingAIAgent` zaten session/serialize/thread/Run'ı `InnerAgent`'a forward ediyor; biz yalnızca `RunCore*` (streaming + non-streaming) override edip şunu yaparız:

1. **Token belirle** (agent'ın token stratejisine göre):
   - `RequestUser` → `IHttpContextAccessor` ile o isteğin `Authorization` header'ından bearer.
   - `ClientCredentials` → `IClientCredentialsTokenProvider`'dan `m2m.client` token'ı (cache'li).
2. **Tool topla** — `IMcpToolProvider.GetToolsAsync(server, url, token)` ile, agent'ın MCP server listesi için.
3. **Options'a birleştir** — gelen `options`'ı `ChatClientAgentRunOptions`'a çevir/zenginleştir; toplanan tool'ları `ChatOptions.Tools`'a ekle (union'lanır).
4. **Delege et** — `InnerAgent.RunStreamingAsync(messages, session, mergedOptions, ct)`.

> Not: Override edilen tam metod adları/tipleri (`RunCoreStreamingAsync` vs.) projenin referansladığı kütüphane sürümüne göre planlama aşamasında birebir doğrulanacak (abstractions 1.9.0'da `RunCoreStreamingAsync(IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken)`).

## 4. Komponentler

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/AgentOrchestrator/RequestAwareAgent.cs` | `DelegatingAIAgent` sarmalayıcı; Run* override + token-stratejisine göre tool enjeksiyonu | Create |
| `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs` | `m2m.client` client_credentials token'ı edin + süresine göre cache/yenile | Create |
| `src/AgentOrchestrator/McpToolProvider.cs` | `GetToolsAsync` artık **açık bearer token** parametresi alır (HttpContext'i kendi okumaz) | Modify |
| `src/AgentOrchestrator/Program.cs` | Inner `ChatClientAgent`'ları tool'suz kur; `RequestAwareAgent` ile sar (token stratejisi + server listesi); Singleton kaydet | Modify |
| `src/AgentOrchestrator/appsettings*.json` + user-secrets | IdentityServer authority + `m2m.client`/secret config | Modify |

### Token stratejisi
Basit bir enum/parametre: `TokenSource { RequestUser, ClientCredentials }`. `public` → `ClientCredentials`, `assistant` → `RequestUser`. Wrapper bu stratejiyle parametrize edilir.

### McpToolProvider değişimi
`GetToolsAsync(string serverName, string url, string? bearerToken, CancellationToken)` — token kararını çağıran (wrapper) verir; sınıf artık `IHttpContextAccessor`'a bağlı olmak zorunda değil. Hata yönetimi (try/catch → boş liste) aynen korunur.

## 5. Veri akışı

**Anonim (public):**
```
POST /public/v1/responses  (token yok)
  → RequestAwareAgent(public).RunCoreStreamingAsync
     → ClientCredentialsTokenProvider → m2m token (catalog.read)
     → McpToolProvider.GetToolsAsync(catalog, m2m token) → catalog tool'ları
     → InnerAgent.RunStreamingAsync(msgs, session, options{Tools=catalog}) → stream
```

**Login (assistant):**
```
POST /assistant/v1/responses  (Authorization: Bearer <user token>)
  → RequestAwareAgent(assistant).RunCoreStreamingAsync
     → HttpContext'ten user token
     → GetToolsAsync(catalog, userToken) + GetToolsAsync(basket, userToken)
     → InnerAgent.RunStreamingAsync(..., options{Tools=catalog+basket}) → stream
```

## 6. Token cache (client_credentials)

`ClientCredentialsTokenProvider` token'ı RAM'de cache'ler; `expires_in`'e göre (örn. -60sn güvenlik payı) yenilenir. Eşzamanlı isteklerde tek seferlik yenileme (lock/SemaphoreSlim). Webapp'teki `TokenService.GetClientAccessTokenAsync` deseni referans alınır (Duende.IdentityModel client). Orchestrator'a ilgili auth paketi eklenecek.

## 7. Hata yönetimi

- **Tool keşfi başarısız** (MCP down / 401): mevcut davranış korunur — exception yutulur, warning loglanır, **boş tool** ile devam (genel sohbet). Agent isteği çuvallamaz.
- **client_credentials alınamazsa:** public agent tool'suz devam eder + warning. Sohbet yine çalışır.
- **assistant'a token'sız istek gelirse** (proxy normalde engeller): tool'suz devam; ileride 401 düşünülebilir (şimdilik kapsam dışı).

## 8. Performans notu

Her chat mesajında MCP handshake + `ListTools` yapılır (istek başına ağ turu). v1 için kabul (doğruluk > gecikme). Olası optimizasyonlar (tool şemasını cache'leyip yalnız token'ı çevirmek, bağlantı havuzu) **kapsam dışı**, gelecekte değerlendirilir.

## 9. Doğrulama

Kullanıcı tercihi gereği TDD bu işin dışında; doğrulama runtime gözlemiyle:
- **D1:** Chat mesajı atınca breakpoint `GetToolsAsync`'e **her istekte** düşüyor (açılışta değil) — singleton-once kırıldı.
- **D2:** Anonim mesaj → catalog sorusu tool çağrısıyla cevaplanıyor (m2m token); dashboard'da 401 warning yok.
- **D3:** Login → "şunu sepete ekle" → basket tool'u kullanıcı token'ıyla tetikleniyor.
- **D4:** Streaming ve çok turlu hafıza (previous_response_id) bozulmadı (DelegatingAIAgent session forward'u).
- Her görevin kapısı `dotnet build` (0 error).

## 10. Kapsam dışı / ertelenen

- Gateway policy'lerinin grant-tipine göre ayrıştırılması (`Password` vs `ClientCredential` şu an ikisi de "geçerli token şart").
- Tool keşfi cache/perf optimizasyonu.
- Chat geçmişinin DB kalıcılığı (ayrı erteleme borcu).

## 11. Açık konular

- Override metod imzalarının kütüphane sürümüyle birebir eşleşmesi (planlamada decompile ile sabitlenecek).
- `m2m.client` secret'ının config kaynağı (user-secrets vs appsettings — dev secret `dev-secret` zaten kaynak Config.cs'te).