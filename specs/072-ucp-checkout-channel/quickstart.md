# Quickstart: UCP Checkout Kanalı — Canlı Doğrulama

Uçtan uca kanıt. Business logic unit testlerde (İlke VI); bu rehber kanalı gerçek stack'te doğrular.
Test yüzeyi = **Claude Desktop** (dış AI platform rolü) → `ucp-sim` (MCP) → mağaza `/ucp` cephesi.

## Ön-koşullar

1. **Aspire AppHost** ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
   (ucp-api + ucp-sim + Order + Identity + Gateway + RabbitMQ + Postgres birlikte).
2. **OpenAI key** ucp-sim/ChatAgent gerektiren servislerde (user-secrets).
3. **PaymentGateway** ayrı repo ayakta ve **iyzico sandbox'a bağlı + çalışan test key** (R7 —
   DOĞRULANACAK). Yoksa PG sandbox key ayarı yapılır (PG repo'sunda; bu feature dokunmaz).
4. **Katalog verisi**: en az bir satılabilir ürün (books.json import'lu) → `UcpCatalogItem` projeksiyonu dolu.
5. Identity seed: `ucp-platform` `client_credentials` istemcisi + `dev.ucp.shopping.checkout` scope.
6. **Claude Desktop**: `ucp-sim` MCP server olarak config'e eklenir (071 Acp.Sim deseni).

## Senaryo 1 — Keşif (US2)

- `GET /.well-known/ucp` → capabilities (checkout) + extensions (fulfillment, discount) +
  payment_handlers + public keys döner.
- `GET /.well-known/oauth-authorization-server` → scope listesinde `dev.ucp.shopping.checkout`.
- `GET /ucp/catalog?q=<kelime>` → satılabilir ürün(ler). Beklenen: en az 1 sonuç.

## Senaryo 2 — Satın alma (US1, ana akış) — Claude Desktop'tan

`ucp-sim` Claude Desktop'a ekli. Sohbetten doğal dille sür:

1. Sim token alır (`client_credentials`, scope checkout).
2. `create` → session `incomplete`, totals hesaplı.
3. `update` → buyer + teslimat adresi + kargo seçimi + (ops) indirim kodu → totals güncel,
   `ready_for_complete`.
4. `complete` (Idempotency-Key ile) → PG/iyzico sandbox charge → `completed` + `order` referansı.
- **Beklenen**: mağazada tek ödemeli sipariş; PG sandbox'ta karşılık gelen tahsilat (test kartı).
- **İdempotency**: aynı complete tekrar → yeni sipariş YOK (SC-002).

## Senaryo 3 — Ödeme başarısızlığı (edge)

- Sandbox'ta reddedilen test kartı/tutar ile complete → `completed` OLMAZ, `messages` hata, sipariş yok (SC-003).

## Senaryo 4 — Webhook (US3)

- Tamamlanan siparişin durumu değişince (`order.confirmed`) `ucp-sim` `/inbox` imzalı webhook alır;
  imza (RFC 9421) doğrulanır. İptalde `order.canceled`. İlk teslim başarısızsa retry gözlenir (SC-004).

## Senaryo 5 — İmza zorlaması (US4, ops)

- `UcpSigningOption.RequireSignatures=false` (sandbox default): imzasız istek geçer.
- `true` yapıp imzasız/bozuk istek → 401 (conformance kanıtı).

## Senaryo 6 — Regresyon (SC-005)

- Mevcut web checkout saga akışının unit/E2E testleri geçmeye devam eder; UCP eklenmesi iç akışı bozmaz.

## Kapsam dışı (bu turda kanıtlanmaz)

- Per-user OAuth authorization_code linking (gelecek); gerçek para/production iyzico; 3DS akışı.