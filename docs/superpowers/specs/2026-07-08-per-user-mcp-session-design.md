# Per-User MCP Session — Kimlik Bind'ini Geri Getirme Tasarımı

**Tarih:** 2026-07-08
**Durum:** Tasarım onaylandı
**Bağlam:** `Catalog.Api` (ve basket) MCP transport'u 403 borcundan kaçmak için `WithHttpTransport(o => o.Stateless = true)` ile stateless yapılmıştı. Bu spec, o borcu geri çevirip **her kullanıcıya kendi stateful MCP session'ını** vererek kimlik bind'ini geri getirir. `2026-06-29-orchestrator-mcp-token-at-invocation-design.md`'nin (handler-level auth, token-at-invocation) üzerine oturur; onu değiştirmez.

## 1. Amaç

MCP session kimlik bind'ini geri getirmek: bir tool çağrısı yalnızca çağıran kullanıcının kimliğine bağlı bir session'da koşsun; boot'ta açılan **anonim paylaşılan session** ortadan kalksın; catalog/basket MCP tekrar **stateful** olsun — hepsi **403 üretmeden**.

## 2. Neden şimdiki stateless model bir açık değil (kapsam netliği)

Her `CallTool` ayrı bir HTTP POST'tur; `TokenInjectingHandler` **her çağrıya** kullanıcının `Authorization` header'ını forward eder ve `[RequiredScope]` middleware **her çağrıda** doğrular. Yani stateless modda bile yetkilendirme kullanıcı-bazında doğrudur; cross-user sızıntı yoktur.

Bu tasarımın kazancı bir **güvenlik açığı kapatmak değil**, **defense-in-depth** ve mimari temizliktir: MCP'nin amaçladığı session-seviyesi kimlik modeline dönmek ve "boot'ta anonim açılıp herkesçe paylaşılan tek session" kokusunu gidermek.

## 3. Kütüphane kısıtı — Singleton kaldırılamaz (doğrulandı)

`Microsoft.Agents.AI.Hosting.OpenAI` decompile edildi. `MapOpenAIChatCompletions(IHostedAgentBuilder)`:

```csharp
// MicrosoftAgentAIHostingOpenAIEndpointRouteBuilderExtensions
AIAgent agent = endpoints.ServiceProvider.GetRequiredKeyedService<AIAgent>(agentBuilder.Name);
// ...agent, endpoint closure'ına yakalanır (MapPost delegate'i)
```

`endpoints.ServiceProvider` = **root provider** ve çözümleme **açılışta bir kez** yapılıp closure'a gömülür. Sonuçlar:
- **Scoped kayıt boot'ta patlar** ("Cannot resolve scoped service from root provider").
- Agent nesnesi (ve içine gömülü tool'lar) **tek örnektir**, tüm kullanıcılarda paylaşılır.

**Dolayısıyla agent zorunlu olarak Singleton'dır.** Bu tasarım Singleton'ı kabul eder; kimlik bind'ini agent nesnesinde değil, **tool çağrısı katmanında** çözer.

## 4. 403'ün kökü ve çözümün mantığı

403 tek sebepten çıkıyordu: **bir session birden çok kimlik arasında paylaşılıyordu** — boot'ta anonim açılan session, login kullanıcının token'ıyla yeniden kullanılınca stateful sunucu "user mismatch" dönüyordu.

Çözüm: **her kimliğe kendi session'ı.** Boot'taki anonim keşif session'ı yalnızca `ListTools` (şema) için kullanılır, hiç `CallTool` yapmaz; her kullanıcı çağrısı kendi token'ıyla taze bir session açar. Hiçbir session iki kimlik arasında paylaşılmadığı için stateful sunucu 403 üretmez.

## 5. Karar: session yaşam döngüsü

**Per-request aç-kapa** (bu iterasyon): her tool çağrısı için taze stateful session açılır, çağrı yapılır, session `await using` ile kapatılır. En basit ve en güvenli (state sızıntısı imkansız); maliyeti her çağrıda SSE session kurma gecikmesidir. Öğrenme projesi için kabul edildi.

**Kapsam dışı (sonra):** kimlik-bazlı session cache + idle TTL. Gecikmeyi düşürür ama cache/eviction/disposal karmaşıklığı getirir; ihtiyaç doğarsa ayrı iterasyonda.

## 6. Tasarım

### 6a. Downstream servisler — `Catalog.Api`, `Basket.Api`
- `WithHttpTransport(o => o.Stateless = true)` **kaldırılır** → default stateful kimlik bind'i geri gelir.
- `MapMcp("/mcp")` **açık kalır** (transport auth gate yok): boot'taki anonim `ListTools` şema keşfi çalışsın diye. Yetki yine handler'daki `[RequiredScope]`'ta kontrol edilir.
- Turkish yorum güncellenir: stateful artık güvenli, çünkü her kimlik kendi session'ını açar ve anonim keşif session'ı hiç `CallTool` yapmaz.

### 6b. ChatAgent — `PerUserMcpTool : AIFunction` (yeni)
Boot keşfinden yakalanan şemayı taşır (`Name`, `Description`, `JsonSchema`) ve çağrıyı çağrı anında açılan kullanıcı-bağlı session'a yönlendirir.

`InvokeCoreAsync(args, ct)`:
1. `McpClient.CreateAsync` ile **taze stateful session** açar — aynı DI `HttpClient` + `TokenInjectingHandler` üzerinden, `ownsHttpClient: false`. Session-create isteği o anki isteğin token'ını taşır → bind kullanıcıya oturur.
2. `client.CallToolAsync(Name, args, ct)`.
3. `await using` ile session'ı kapatır.
4. Sonucu döner. Çağrı başarısızsa (session açılamaz / token yok / CallTool hata) **hata sonucu** döner ki model görüp yanıtlayabilsin (sessiz yutma yok).

### 6c. ChatAgent — `McpToolProvider`
- `GetToolsAsync` yine boot'ta **anonim `ListTools`** yapar; ham `McpClientTool` yerine her şema için bir `PerUserMcpTool` üretir.
- Allowlist filtresi ve "allowlist'te olup sunucuda olmayan tool" uyarısı aynen korunur.
- Keşif client'ı `ListTools`'tan sonra **dispose** edilir (bugün edilmiyor; stateful'da temizlik gerekir).

### 6d. ChatAgent — `Program.cs`
- Kayıtlar büyük ölçüde aynı: `HttpClient` + `TokenInjectingHandler` + `RemoveAllResilienceHandlers` **korunur** (per-call session'lar da uzun-ömürlü SSE GET açar; resilience handler'ının `TotalRequestTimeout`'u bu bağlantıyı keser).
- Tek fark: agent'a giren tool'lar artık `PerUserMcpTool`.
- Eşzamanlılık: aynı `HttpClient`'ı eşzamanlı per-user session'lar paylaşabilir. `HttpClient` thread-safe; session ayrımı SDK'nın client-başına yönettiği `Mcp-Session-Id` header'ıyla yapılır, `HttpClient` üzerinden değil.

## 7. Veri akışı (assistant, `add_to_cart`)

```
POST /assistant/v1/chat/completions
  → MapOpenAIChatCompletions endpoint (Singleton agent, closure'da)
  → agent.RunAsync
  → model add_to_cart çağırır
  → PerUserMcpTool.InvokeCoreAsync
      → basket'e kullanıcı token'ıyla TAZE stateful session (McpClient.CreateAsync)
      → CallToolAsync("add_to_cart", args)
          → [RequiredScope(BasketWrite)] handler middleware doğrular
      → session await using ile kapanır
  → sonuç modele döner → streaming yanıt
```

Boot'ta: her agent factory `CollectTools` → her server için anonim `ListTools` → `PerUserMcpTool` listesi → dispose discovery client. Public agent yalnız catalog `search_products`; assistant catalog `search_products`+`get_product` ve tüm basket tool'ları (allowlist değişmez).

## 8. Hata yönetimi

- **Boot keşfi başarısız:** bugünkü davranış korunur — o server sessizce atlanır (`[]`), log warning.
- **Per-call session açılamaz / token yok / CallTool hata:** tool bir hata sonucu döndürür (model görsün); exception sessizce yutulmaz.

## 9. Doğrulama (test projesi yok → manuel)

`dotnet run --project src/AppHost`, sonra:
1. **Public agent (anonim):** `search_products` çalışır.
2. **Assistant agent (login):** `add_to_cart` çalışır; sepet güncellenir.
3. **Catalog/Basket logları:** stateful session açılır; **403 YOK**.
4. **Eşzamanlılık:** iki farklı kullanıcı aynı anda çağrı yaptığında biri diğerinin session'ını görmez/etkilemez.

## 10. Kapsam dışı (YAGNI)

- Kimlik-bazlı session cache + TTL (gerekirse ayrı iterasyon).
- Chat history persistence (ayrı borç; `chat-history-storage`).
- `MapOpenAI*`'ı bırakıp endpoint'leri elle yazarak per-request gerçek agent (devasa maliyet, reddedildi).
- Konuşma-thread'i izolasyonu (`UseClaimsBasedSessionIsolation`) — farklı bir tehdit, bu spec kapsamı dışında.

## 11. Etkilenen dosyalar

- `src/services/catalog/Catalog.Api/Program.cs` — stateless override kaldır, yorum güncelle.
- `src/services/basket/Basket.Api/Program.cs` — aynı (satır 69'daki `Stateless = true` override kaldırılır, yorum güncellenir).
- `src/ChatAgent/PerUserMcpTool.cs` — yeni.
- `src/ChatAgent/McpToolProvider.cs` — `PerUserMcpTool` üret, discovery client'ı dispose et.
- `src/ChatAgent/Program.cs` — agent'a `PerUserMcpTool` ver (kayıtlar büyük ölçüde aynı).