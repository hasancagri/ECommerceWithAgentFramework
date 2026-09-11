# Quickstart: Tek Müşteri MCP Fasadı — Canlı Doğrulama

Uçtan uca kanıt. Saf birimler (routing/süzme) unit testlerde (İlke VI); bu rehber fasadı gerçek stack'te
doğrular. Test yüzeyi = **Claude Desktop** → tek fasad MCP → alt BC'ler.

## Ön-koşullar

1. **Aspire AppHost** ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
   (mcp-gateway + basket/order/customer/payment/storefront/catalog + Identity + Gateway + RabbitMQ + Postgres).
2. Katalog verisi: en az bir satılabilir ürün (books.json import'lu).
3. Identity seed: `external-customer-agent` (public+PKCE, müşteri scope demeti) + `mcp-gateway-discovery`
   (client_credentials).
4. **Claude Desktop**: TEK MCP kaydı — fasad `/mcp` (gateway üzerinden). mcp-remote **çalışan sürüm**
   (bkz [[mcp-remote-issuer-slash-gotcha]] — katı sürüm oynamalarına dikkat).

## Senaryo 1 — Anonim keşif + sepet (girişsiz, US1)

- Claude Desktop'ta tek fasad bağlı; **giriş sorulmadan** tool listesi gelir (birden çok BC'nin müşteri
  tool'ları tek listede).
- Doğal dille: "roman ara" → `search_products` (storefront/catalog, anonim) sonuç döner.
- "İlk kitabı sepete ekle" → `add_to_cart` (basket, anonim X-User-Key) — **giriş istenmez**.
- Beklenen: tek MCP, giriş sayısı bu aşamada **0**.

## Senaryo 2 — Checkout'ta tek step-up login + sepet devri (US1)

- "Satın al / siparişi tamamla" → ilk korumalı çağrı **401 → OAuth dansı** → tarayıcıda **tek** login/consent.
- Login sonrası: anonim sepet kullanıcıya devredilir (kalemler korunur); adres seç/ekle (`add_address`/
  seçim), kayıtlı kart seç, sipariş tamamla.
- Beklenen: oturum boyunca toplam giriş **1**; ikinci giriş sorulmaz.
- Yeni kart gerekiyorsa: fasad kart alanı toplamaz → PG hosted form bağlantısı sunar.

## Senaryo 3 — Doğru yönlendirme (SC-004)

- `add_to_cart` → basket'te, `place_order` → order'da gerçekleşir (yanlış-yönlendirme %0). Aspire
  loglarından/DB'den doğrula.

## Senaryo 4 — Dayanıklılık (US3, SC-005)

- Bir BC'yi durdur (ör. reviews). Fasadda tool listesi → o BC'nin tool'ları yok, diğerleri çalışır.
- BC'yi yeniden başlat → tool listesi tekrar istenince tool'ları geri gelir (yeniden bağlanma YOK).

## Senaryo 5 — Yüzey ayrımı (US2, SC-006)

- Müşteri `/mcp` ucunda yönetim tool'ları GÖRÜNMEZ. Ayrı `/mcp-admin` fasadı (external-admin-agent, ayrı
  login) yalnız yönetim tool'larını gösterir.

## Kapsam dışı (bu turda kanıtlanmaz)

- ChatAgent söküm (ayrı feature). UCP REST kanalı (ayrı persona; değişmedi).