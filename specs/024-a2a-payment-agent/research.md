# Research: 024 A2A Installment Quote

Phase 0. Tüm NEEDS CLARIFICATION çözüldü. A2A istemci API'si birincil kaynaklardan
doğrulandı (MAF 1.13 + A2A v1); alpha paket olduğundan isimler teyit edildi.

## D1 — A2A istemci paketleri

- **Decision:** `Microsoft.Agents.AI.A2A` (**prerelease**, `1.13.0-preview.*` — core
  `Microsoft.Agents.AI 1.13.0` ile lockstep) + alt SDK `A2A` (`1.0.0-preview2`, açıkça pinle).
  Sunucu paketleri (`A2A.AspNetCore`, `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`) GEREKMEZ —
  onlar A2A agent YAYINLAMAK içindir; biz istemciyiz. Onlar uzak `PaymentGateway` solution'ında.
- **Rationale:** Resmî MAF senaryosu; uzak A2A agent'ı yerel `ChatClientAgent`'a tool olarak
  bağlamak first-class (örnek: `samples/05-end-to-end/A2AClientServer`).
- **Alternatives:** Ham JSON-RPC/HTTP elle — reddedildi (SDK card-resolve + AIAgent glue veriyor).
- **Not:** `Directory.Packages.props`'a eklenir (CPM). Prerelease olduğu için `--prerelease`.

## D2 — Uzak agent'a bağlanma + tool'a çevirme

- **Decision:**
  ```csharp
  using A2A; using Microsoft.Agents.AI;
  var resolver = new A2ACardResolver(new Uri(paymentAgentUrl), httpClient: a2aHttpClient);
  AgentCard card = await resolver.GetAgentCardAsync();      // yetenek dogrulamasi
  AIAgent remote = await resolver.GetAIAgentAsync();        // card resolve + AIAgent
  AITool installmentTool = remote.AsAIFunction();           // AIFunction : AITool
  ```
  `AsAIFunction()` = `Microsoft.Agents.AI/AgentExtensions.cs` extension; agent'ı tek NL
  `InvokeAgentAsync(string query)` fonksiyonuna sarar (skill-by-name DEĞİL). `tools:` listesine
  assistant'a eklenir. Uzak agent NL sorguyu kendi içinde `installment_quote` skill'ine yönlendirir.
- **Rationale:** Mevcut `CollectTools(...)` + `ChatClientAgent(..., tools)` desenine bire bir oturur.
- **AgentCard discovery:** default `/.well-known/agent-card.json` (v1; `agentCardPath` override).
  Skill id `installment_quote` (contract). Boot'ta `card.Skills`'te yoksa tool eklenmez (US2).

## D3 — Auth: ŞİMDİLİK YOK (merchant key ertelendi)

- **Decision:** Bu iterasyonda **merchant key / user token GÖNDERİLMEZ** (user 2026-08-04;
  uzak taraf henüz yok). Çağrı auth header'sız. Yine de A2A istemcine **kendi named HttpClient**
  verilir (`A2ACardResolver`/`A2AClient` ctor `HttpClient?` alır) — SSE resilience-muafiyeti için
  gerekli, merchant key'den bağımsız. Auth handler ileride buraya takılır (genişleme noktası).
- **Rationale:** .NET'te sanksiyonlu auth = custom HttpClient/`DelegatingHandler` (mevcut
  `TokenInjectingHandler`/gRPC `BearerForwardingHandler` deseni). Eklendiğinde scope-auth (İlke V).
- **PCI:** Her hâlükârda PAN/CVV/token gövdede YOK; yalnız amount + BIN.

## D4 — Transport / SSE resilience

- **Decision:** A2A named HttpClient'ı **standart resilience'tan muaf** tut (MCP client'larıyla
  aynı `RemoveAllResilienceHandlers()` + cömert timeout). Binding v1 default HTTP+JSON; gerekirse
  `A2AClientOptions.PreferredBindings` ile sabitlenir.
- **Rationale:** A2A streaming = SSE (uzun-ömürlü GET); global timeout/resilience akışı keser —
  MCP'de yaşanan sorunun aynısı (Program.cs:65-74'te zaten bu muafiyet var).

## D5 — Graceful-degrade (US2 / FR-006)

- **Decision:** A2A url yapılandırılmamış → tool hiç eklenmez, assistant diğer tool'larla açılır.
  Url var ama card-resolve/erişim başarısız → fail-open (agent yine kalkar, tool yok). Çağrı anı
  hatası try/catch → "taksit şu an alınamıyor", exception sızmaz.
- **Rationale:** Uzak taraf ayrı solution, eşzamanlı geliştiriliyor; yokluğu asistanı bozmamalı.
- **Alternatives:** Boot'ta card-resolve zorunlu (fail-fast) — reddedildi (US2/SC-002'yi bozar).

## D6 — BIN yakalama (Customer BC)

- **Decision:** `SavedCard.Bin` (ilk 6 hane) + `TokenizeResult.Bin`; `SimulatedCardTokenizer`
  PAN'ın ilk 6'sını döndürür (satır 13'te digits zaten var). Default kartın BIN'ini veren okuma
  slice/MCP tool. Bkz. `data-model.md`.
- **Rationale:** Taksit banka-özel; BIN bankayı belirler. BIN hassas değil, saklanır; PAN/CVV değil.
- **Alternatives:** Runtime'da vault'tan BIN sor (token→BIN) — reddedildi (ekstra round-trip + ifşa).

## D7 — Intent ayrımı + sepet toplamı

- **Decision:** `Prompts.AssistantInstructions`'a taksit-intent maddesi: "taksit iste →
  `get_basket` ile toplam + default kart BIN → installment tool". Ödeme/charge niyeti nazik red.
- **Rationale:** Mevcut MCP `get_basket` (BasketTools.GetBasket) toplamı verir; yeni yetenek yok.

## Çözülen belirsizlikler

- Girdi = tutar + default kart BIN (tutar-only'den revize; user 2026-08-04). PAN/CVV asla.
- Sonuç = kullanıcının kendi kartının bankasının tablosu (bankalar-arası kıyas DEĞİL).
- Auth şimdilik yok (merchant key ertelendi); HttpClient yine enjekte edilir.

## Kalan riskler (düşük)

- Prerelease API sürüm eşlemesi: `A2A`'yı `1.0.0-preview2`'ye açıkça pinle.
- Uzak Gateway'in card path'i `agent.json` ise `agentCardPath` override gerekir.
- `GetAgentCardAsync` metod adı v1'de teyit; farklıysa `GetAIAgentAsync` sonrası agent
  metadata'sından skill doğrulanır (implement aşamasında netleşir).