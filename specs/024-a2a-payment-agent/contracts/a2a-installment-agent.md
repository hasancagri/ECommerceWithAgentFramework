# Contract: Uzak A2A PaymentAgent — Taksit Sorgulama

**Tür:** Agent2Agent (A2A) protokol kontratı. Bu repo **istemci**; uzak taraf (ayrı
solution `hasancagri/PaymentGateway`) **sunucu**. Kontrat-önce sabitlenir; uzak taraf yok
iken bu isimler değişmez (FR-007).

## AgentCard (uzak taraf yayınlar)

- **Discovery URL:** `{PaymentGatewayA2AUrl}/.well-known/agent-card.json` (A2A v1 convention;
  uzak taraf `agent.json` sunarsa istemci `agentCardPath` ile override eder).
- **Sabit alanlar (kontrat):**
  - `name`: `payment-gateway-agent`
  - `skills[].id`: **`installment_quote`** (delege edilen tek yetenek adı — SABİT).
- Uzak taraf başka skill de yayınlayabilir; bu feature yalnız `installment_quote`'u kullanır.
- **İstemci doğrulaması:** boot'ta `A2ACardResolver.GetAgentCardAsync()` ile kart çekilir;
  `card.Skills` içinde `installment_quote` YOKSA tool eklenmez (graceful-degrade, US2).
- **Çağrı biçimi:** istemci `remote.AsAIFunction()` ile agent'ı tek NL-fonksiyona sarar;
  skill-by-name çağırmaz — assistant NL sorgu gönderir, uzak agent skill'e yönlendirir.

## Skill: `installment_quote`

Read-only taksit sorgusu. **Girdi = tutar + (opsiyonel) BIN.** PAN/CVV/token ASLA.

### Girdi (assistant → uzak agent)

```json
{
  "amount": 1499.90,
  "currency": "TRY",
  "bin": "552879"
}
```

- `amount` (decimal, zorunlu): sepet toplamı. Basket MCP `get_basket`'ten türetilir (FR-002).
- `currency` (string, zorunlu): ISO-4217; sistemde `TRY`.
- `bin` (string, opsiyonel): default kartın ilk 6 hanesi. Yoksa uzak agent genel/örnek
  taksit tablosu döner (FR-002a fallback). **Hassas değil.** Tam PAN/orta haneler/CVV YOK.

### Çıktı (uzak agent → assistant)

```json
{
  "bank": "Garanti BBVA",
  "networkBrand": "Visa",
  "currency": "TRY",
  "options": [
    { "installmentCount": 1, "perInstallmentAmount": 1499.90, "totalAmount": 1499.90, "commissionRate": 0.0 },
    { "installmentCount": 3, "perInstallmentAmount": 508.30, "totalAmount": 1524.90, "commissionRate": 1.5 },
    { "installmentCount": 6, "perInstallmentAmount": 262.48, "totalAmount": 1574.88, "commissionRate": 4.0 }
  ]
}
```

- `bank`: BIN'den çözülen banka (BIN verildiyse). BIN yoksa null/"Genel".
- `options[]`: taksit sayısı, taksit başına tutar, toplam, komisyon oranı (%).
- Sonuç kullanıcının **kendi kartının bankasının** tablosudur — bankalar-arası kıyas değil.
- Assistant alanları **uydurmaz** (FR-003); eksik alan gelirse "bilgi eksik" der.

### Hata / boş durumlar

- Uzak agent erişilemez/yapılandırılmamış → assistant graceful-degrade (FR-006, US2):
  "taksit bilgisi şu an alınamıyor". Exception sızmaz.
- `options` boş (ör. tutar taksite uygun değil) → "uygun taksit seçeneği yok" (Edge Case).
- Kısmi/biçimsiz yanıt → tutarlı alanları göster, eksiği bildir; alan uydurma.

## Auth (FR-008) — ŞİMDİLİK YOK

- **Merchant key / user token GÖNDERİLMEZ** (bu iterasyon). Uzak taraf henüz yok; auth
  iletimi ertelendi. Çağrı auth header'sız gider.
- Yine de **kendi named HttpClient**'ımız verilir (SSE resilience-muafiyeti için zorunlu,
  merchant key'den bağımsız). Auth handler'ı ileride buraya takılacak genişleme noktası
  (`TokenInjectingHandler` / gRPC `BearerForwardingHandler` deseni).
- Eklendiğinde scope-tabanlı kalır (rol yok, İlke V); uzak taraf merchant-scope, son-kullanıcıyı
  bilmez. Her hâlükârda PAN/CVV/token bu kanaldan geçmez; yalnız amount + BIN.

## Kapsam dışı (sonraki feature)

- `charge` (çekim), `tokenize` (kart ekleme), refund. Bu contract yalnız `installment_quote`.