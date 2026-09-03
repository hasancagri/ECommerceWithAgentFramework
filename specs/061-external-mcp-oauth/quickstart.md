# Quickstart: Dış Agent MCP Erişimi — canlı doğrulama (061)

## Önkoşullar

- Aspire AppHost ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Identity.Server HTTPS canlı (dev cert geçerli); gateway portunu Aspire dashboard'dan not al.
- Claude Code kurulu (bu repo dışında herhangi bir dizinden de olur).

## 1. Keşif zincirini elle doğrula (agent'sız)

```bash
# 401 + WWW-Authenticate challenge (korumalı servis)
curl -i http://localhost:<gw>/mcp/basket
# Beklenen: 401, WWW-Authenticate: Bearer resource_metadata="..."

# Metadata dokümanı
curl -s <resource_metadata URI> | jq .
# Beklenen: resource + authorization_servers + scopes_supported

# AS discovery'de registration_endpoint
curl -sk https://localhost:<idp>/.well-known/openid-configuration | jq .registration_endpoint
# Beklenen: /connect/register adresi

# Anonim yüzey anonim kaldı (regresyon)
curl -i http://localhost:<gw>/mcp/storefront   # 401 DEĞİL (MCP protokol yanıtı)
```

## 2. Claude Code'u bağla (P1 + P2 seremonisi)

```bash
claude mcp add --transport http store-basket http://localhost:<gw>/mcp/basket
claude mcp add --transport http store-order http://localhost:<gw>/mcp/order
claude mcp add --transport http store-customer http://localhost:<gw>/mcp/customer
claude mcp add --transport http store-storefront http://localhost:<gw>/mcp/storefront
# Claude Code içinde: /mcp → store-basket → Authenticate
```

Beklenen: tarayıcı açılır → Identity.Server login (yeni kullanıcı: Register linki → customer
rolü) → consent sayfası scope listesiyle → Onayla → Claude "authenticated" gösterir.
Reddet senaryosu: consent'te Reddet → Claude hata gösterir, mağazada yan etki yok (SC-005).

## 3. E2E alışveriş — %100 yazışma (FR-010, SC-002)

Claude Code sohbetinde sırayla:

1. "Mağazada bilim kurgu kitabı ara" → `search_storefront_products` sonuç döner.
2. "İlkini sepete ekle" → `add_to_cart` başarılı; "sepetimi göster" → kalem görünür.
3. "Siparişi ver" → `place_order` sipariş numarası döner.
4. "Siparişlerimi göster" → `get_orders` yeni sipariş listede.

Hiçbir adımda mağaza ekranı açılmaz; tool hataları düz metin olarak görünür olmalı.

## 4. Sessiz yenileme + iptal (P3, FR-008/FR-009)

- Access token süresini kısa ayarla (dev) → süre dolunca yeni tool çağrısı ekransız başarılı
  (refresh çalıştı, SC-003).
- `POST /connect/revocation` ile refresh token'ı iptal et (veya Claude'da disconnect) →
  sonraki çağrı 401; yeniden Authenticate aynı seremoniyle çalışır.

## 5. Regresyon (SC-004)

- WebApp: login → sepet → checkout akışı değişmemiş.
- ChatAgent: PUBLIC arama (token'sız storefront MCP) + ASSISTANT sipariş akışı PASS.
- UserKey: `curl -H "X-User-Key: <key>" http://localhost:<gw>/mcp/basket` eski davranışla çalışır.
- `dotnet test` yeşil.

## Bilinen sınırlar (bilinçli)

- claude.ai / Claude Desktop connector'ı public HTTPS ister → tünel sonraki dilim.
- Görsel yalnız URL olarak döner (serving onarımı kapsam dışı).
- DCR ucunda rate-limit yok (localhost dilimi); public aşamada eklenecek.