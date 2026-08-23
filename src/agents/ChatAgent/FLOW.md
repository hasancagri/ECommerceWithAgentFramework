# ChatAgent — Domain Süreci

**BC ne yapar:** Kullanıcının doğal-dil mesajını alır, LLM'e downstream BC'lerin `/mcp` okuma/eylem
tool'larını seçtirir, kullanıcının token'ıyla çağırır; taksit/çekim gibi ödeme işini uzak A2A
PaymentAgent'a devreder. Kendi DB'si yok — sohbet süreci sahibi, veri sahibi değil.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-tool-persona) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Persona başına bir agent boot'ta kurulur.** public/assistant/     `(AddAIAgent → ChatClientAgent)`
   admin — her biri Singleton `ChatClientAgent`; kişiye özel
   değil, kimlik çağrı anında enjekte edilir.
2. **İzinli MCP tool'ları boot'ta anonim keşfedilir.** Persona'nın      `(CollectTools → IMcpToolProvider)`
   allowlist'i `ListTools` ile filtrelenir; bilinmeyen tool asla
   eklenmez (fail-safe).
3. **Ödeme aracı uzak A2A karttan bağlanır.** PaymentAgent card'ı       `(PaymentAgentInstallmentTool`
   çözülür, skill'leri doğrulanır, tek NL fonksiyonuna sarılır;          ` → A2ACardResolver → AsAIFunction)`
   uzak taraf yoksa fail-open (araç eklenmez, boot çökmez).
4. **Kullanıcı mesajı persona endpoint'ine düşer.** WebApp BFF          `(MapOpenAIResponses)`
   token'ı taşıyarak /public|/assistant|/admin'e proxy'ler;
   süreklilik = istemci transkripti + BFF input dizisi (stateless).
5. **LLM niyeti ayırt edip tool seçer.** Arama/sepet/sipariş/stok        `(Prompts.AssistantInstructions)`
   ayrımını prompt yapar; imperatif `CallTool` YOK — tool'u model seçer.
6. **Seçilen tool kullanıcının token'ıyla taze session açar.** Her      `(PerUserMcpTool`
   çağrı named-client'a takılı handler'la o an ki token'ı forward         ` → TokenInjectingHandler)`
   eder → bind kullanıcıya oturur; yetki downstream `[RequiredScope]`.
7. **Taksit sorgusu ödeme bağlamıyla A2A'ya delege edilir.** Vault      `(CustomerTools.GetPaymentContext`
   token + buyer Customer'dan alınır, A2A isteğine verbatim gider;        ` → QuoteInstallmentsSkill)`
   PAN/CVV asla. Seçenekler kullanıcıya bilgi olarak döner.
8. **Çekim + sipariş TEK adımda sunucuya bırakılır.** Onaydan sonra     `(OrderTools.PlaceOrder / ChargeSkill)`
   yalnız taksit sayısı + seçili kart iletilir; tutar/adres/kalem
   sunucuda oluşur. Sonuç mesajı kullanıcıya olduğu gibi iletilir.

## Domain kuralları (süreci yöneten değişmezler)

- **Agent Singleton, kimlik çağrı anında.** Framework agent'ı boot'ta yakalar; per-user davranış = `TokenInjectingHandler`.
- **MCP yalnız agent üzerinden.** Tool'u LLM prompt'la seçer; agent-dışı imperatif `CallTool` yok — `PerUserMcpTool` sarar.
- **PAN/CVV asla LLM'de/A2A'da.** Kart ekleme/silme chat'te yasak; ödeme yalnız vault token + buyer ile (`GetPaymentContext`).
- **Ödeme uzak BC'ye delege.** Taksit/çekim Customer MCP'de değil, A2A `PaymentAgent` skill'lerinde (`ChargeSkill`).
- **Fail-open dış bağımlılık.** MCP keşfi / A2A card erişilemezse araç atlanır, boot çökmez (graceful-degrade).

## Sınır (bu BC'nin dokunmadığı)

Fiyat/stok/sipariş yazımı, ödeme yürütmesi burada DEĞİL — okuma+eylem downstream BC'lerde, çekim uzak
PaymentGateway'de. ChatAgent yalnız niyeti tool'a çevirir; iş mantığı ve kalıcılık başka BC'nin.
