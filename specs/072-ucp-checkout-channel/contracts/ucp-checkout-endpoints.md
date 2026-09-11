# Contract: UCP Checkout Session Uçları

Dış-protokol REST yüzeyi. Gateway `/ucp/{**}` ile proxy'ler. Tümü OAuth scope
`dev.ucp.shopping.checkout` ile korunur (`RequireAuthorization`). İstek bütünlüğü RFC 9421 imzasıyla
(bkz [ucp-webhook.md](./ucp-webhook.md) aynı imza şeması) — zorlama `UcpSigningOption.RequireSignatures`
bayrağına bağlı. Ortak header'lar: `UCP-Agent` (platform profil URL'i), `Signature`, `Signature-Input`,
`Content-Digest`, `Idempotency-Key` (complete).

Yanıt gövdesi UCP `checkout.json` şemasına uyar (SC-006). Hata → Result + `messages[]` (UCP mesaj
formatı); HTTP kodları: 200 OK, 400 doğrulama, 401/403 yetki/imza, 404 session yok, 409 idempotency/durum.

## POST /ucp/checkout_sessions — create

İstek: `{ currency, line_items:[{product_id, quantity}], buyer?, links? }`
Yanıt: tam session, `status: "incomplete"`, hesaplanmış `totals`, `expires_at` (+6h).

## POST /ucp/checkout_sessions/{id} — update (tam değişim)

İstek: `{ line_items?:[...] (TAM liste), buyer?, fulfillment?:{selected_option_id, destination_id},
discounts?:{codes:[...]} }`
Davranış: `line_items` verilirse mevcut listeyi **replace** eder; kargo/indirim totals'a yansır.
Yanıt: güncel session; hazırsa `status: "ready_for_complete"`, değilse `incomplete` + `messages`.

## GET /ucp/checkout_sessions/{id} — get

Yanıt: session mevcut durumu.

## POST /ucp/checkout_sessions/{id}/complete — complete

Header: `Idempotency-Key` zorunlu.
Ön-koşul: `status == ready_for_complete` + süre dolmamış.
Davranış: totals tazelenir → gRPC `CreateExternalOrder` (Order charge PG/iyzico sandbox + AlreadyCaptured)
→ başarılıysa `status: "completed"` + `order`. Ödeme başarısız → `completed` OLMAZ, `messages` + sipariş yok.
İdempotent: aynı key/session tekrarında yeni sipariş üretmez, mevcut sonucu döner.

## POST /ucp/checkout_sessions/{id}/cancel — cancel

Davranış: terminal-olmayan durumdan `status: "canceled"`. Tamamlanmış session iptal edilmez (409).

## Keşif/katalog (ayrı extension'lar)

- `GET /.well-known/ucp`, `GET /.well-known/oauth-authorization-server` → [ucp-discovery-profile.md](./ucp-discovery-profile.md)
- `GET /ucp/catalog/{id}` (lookup), `GET /ucp/catalog?q=` (search) → `UcpCatalogItem` alanları.