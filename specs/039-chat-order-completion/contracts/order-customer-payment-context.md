# Contract: Order → Customer Ödeme Bağlamı (yapısal)

Order.Api, buyer + vaultToken + varsayılan adresi **yapısal** kanaldan alır. Bugün `get_payment_context`
yalnız MCP (agent yüzeyi); Order.Api agent değil → imperatif MCP çağıramaz (İlke I). Bu yüzden yapısal
ikiz (gRPC tercih — Order↔Basket ile aynı desen).

## Kaynak

- Mevcut mantık: Customer.Api `Domains/Wallets/Features/Agents/GetPaymentContextForAgent.cs`
  (`PaymentContextView`). Aynı handler yapısal uçtan da sunulur.

## İstek

| Param | Tip | Not |
|-------|-----|-----|
| userId | Guid | çağıran (Order makine token'ıyla iletilir) |
| cardId | Guid? | seçilen kart; null → varsayılan |

## Yanıt (`PaymentContextView`, mevcut 14 alan)

| Grup | Alanlar |
|------|---------|
| Merchant/kart | MerchantId, VaultToken, CardBrand, CardLast4, CardIsDefault |
| Buyer | BuyerName, BuyerSurname, BuyerEmail, BuyerGsmNumber, BuyerIdentityNumber |
| Adres/lokasyon | BuyerRegistrationAddress, BuyerCity, BuyerCountry, BuyerIp |

## Kullanım (Order.Api)

- Buyer 11 alan → PG charge isteği (VERBATIM) + Order alıcı bilgisi.
- Adres 3 alan → Order `AddressDto` eşlemesi.
- VaultToken + MerchantId → PG charge.
- Adres/merchant/kart yoksa Customer NotFound → place_order reddedilir (FR-009; 038 tutarlı).

## Auth

- Order makine kimliği (client-credentials, order-saga benzeri); kullanıcı bearer arka planda yok.
- Scope: Customer context okuma scope'u.

## Not

- Sandbox sabitleri (TCKN "11111111111", Country "Turkey", IP dummy) mevcut 033 davranışı — korunur.
- VaultToken + merchantId asla UI/LLM'e sızmaz; yalnız yapısal kanal + PG charge.