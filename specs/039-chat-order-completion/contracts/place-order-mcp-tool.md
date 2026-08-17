# Contract: `place_order` MCP Tool (Order.Api agent yüzeyi)

Agent-merkezli tetikleyici. LLM yalnız bunu seçer + iki parametre verir; gerisi sunucu.

## MCP Tool

- **Name**: `place_order`
- **Type**: `[McpServerTool]` — Order.Api `OrderMcpTools.cs` (mevcut `get_orders` yanına)
- **Slice**: `Domains/Orders/Features/Agents/PlaceOrderForAgent.cs` (izole; Commands'ı IMessageBus
  ile çağırmaz — [[agent-features-folder-convention]])
- **Auth**: kullanıcı token'ı + `order.write` scope
- **Allowlist**: ChatAgent `ConstValues.assistantAgentTools` + `OrderTools.PlaceOrder = "place_order"`
- **Prompt**: `AssistantInstructions`'a "SİPARİŞ VERME" kuralı — kullanıcı onaylayınca `place_order`
  çağır; **amount/buyer/kalem verme** (sunucu sentezler); yalnız `cardId?` + `installment`.

## İstek (LLM → tool)

| Param | Tip | Not |
|-------|-----|-----|
| cardId | Guid? | seçilen kart; null → varsayılan |
| installment | int | taksit sayısı (1 = tek çekim); mevcut taksit-sorgu adımından |

**LLM'in VERMEDİĞİ** (sunucu türetir): userId (token), amount, buyer, adres, sepet kalemleri,
correlationKey, vaultToken.

## Yanıt (tool → LLM → kullanıcı)

| Alan | Not |
|------|-----|
| outcome | `created` / `payment_failed` / `pending` / `rejected` |
| orderCode | created ise sipariş numarası |
| itemCount, totalPrice | özet |
| message | kullanıcıya gösterilecek (pending ise "ödemen alınmış olabilir, kontrol ediliyor") |

## Sunucu davranışı (handler — LLM'siz, Yol 2)

1. Basket `GetBasketItems` (boş → `rejected` / ORDER_BASKET_EMPTY)
2. Customer payment-context (yapısal) → buyer+vaultToken+adres (yok → `rejected`)
3. CorrelationKey.Create(userId, basketId, contentHash, installment)
4. PaymentAttemptSaga.Start(key) → PG charge (idempotent key)
5. success → Order.Create + StartCheckout (CheckoutSaga) → `created`
6. failed → `payment_failed`; ambiguous → `pending` + reconcile zamanla
7. Aynı key ile tekrar çağrı → var olan saga (yeni çekim yok)