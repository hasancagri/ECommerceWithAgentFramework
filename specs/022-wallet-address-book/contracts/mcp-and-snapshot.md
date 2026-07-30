# MCP (okuma-yalnız) + Checkout Snapshot Kontratları

## MCP tool'ları (yalnız okuma)

`*McpTools.cs` ince sarmalayıcı: aynı `Features/Agent/` query'sini `IMessageBus` ile çağırır,
iş mantığı eklemez. Kullanıcı `CurrentUser.Load(http.HttpContext!.User).Id` ile çözülür.

| Tool | Sardığı query | Döndürür | Not |
|------|---------------|----------|-----|
| `list_cards` | `Agent.GetCards.GetCardsQuery(userId)` | CardView[] | brand+last4+expiry+label+isDefault; **token/PAN yok** |
| `list_addresses` | `Agent.GetAddresses.GetAddressesQuery(userId)` | AddressView[] | adres alanları + isDefault |

- **Yazma tool'u YOK**: ekle/sil/varsayılan REST/WebApp'te. Kart ekleme **asla** tool değil
  (FR-019, ham PAN LLM'e girmez).
- Doğal dil referans ("…1111", "ev adresim") tekile çözülemezse agent kullanıcıdan açık seçim
  ister; sistem sessizce yanlış kayıt seçmez (FR-018). Bu davranış agent tarafında; tool yalnız
  liste döner.

## Checkout Snapshot kontratı (bu feature UYGULAMAZ — yalnız belgeler)

Checkout/ödeme akışı ayrı feature. Bu feature Wallet/AddressBook'u **referanslanabilir** kılar;
checkout seçilen kayıttan siparişe değerleri **kopyalar** (snapshot). US3/FR-016/017.

**Adres snapshot** (Order BC kendi `Address` VO'suna kopyalar):
`{ province, district, street, zipCode, line }` — checkout anındaki değerler dondurulur.

**Kart snapshot** (siparişte görünen kart bilgisi):
`{ brand, last4 }` — token siparişe **kopyalanmaz** (charge ödeme akışının işi; ayrı feature).

**Dondurma garantisi**: kopyalama sonrası kaynak kayıt değişse/silinse geçmiş sipariş
snapshot'ı değişmez (SC-004). Order zaten kendi VO'sunu tutar → doğal olarak izole.

**Nasıl erişilir** (ayrı feature'ın kararı, burada sabitlenmez): WebApp BFF seçili id'leri
Customer API'den okuyup Order'a geçirebilir, ya da Order MCP `list_*` ile okur. Bu feature
yalnız okuma yüzeyini (REST + MCP) sağlar.