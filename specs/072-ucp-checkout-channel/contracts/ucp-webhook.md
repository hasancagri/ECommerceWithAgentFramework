# Contract: UCP Giden Webhook + RFC 9421 İmza Şeması

Mağaza, sipariş olaylarını platforma imzalı POST ile bildirir. Aynı imza şeması gelen istek
doğrulamasında da kullanılır (ters yön).

## Olaylar

- `order.confirmed` — Order `OrderCompleted` fanout'undan.
- `order.canceled` — Order `OrderCanceledEvent` (additive) fanout'undan.

## POST {platform_inbox_url}

Header'lar (RFC 9421 + RFC 9530):

- `Content-Digest: sha-256=:<base64(SHA256(body))>:`
- `Signature-Input: sig1=("@method" "@target-uri" "content-digest");created=<ts>;keyid="<kid>";alg="<ecdsa-p256-sha256|ed25519>"`
- `Signature: sig1=:<base64(signature)>:`
- `UCP-Agent: <mağaza profil URL'i>`

Gövde: `{ "type": "order.confirmed", "order_ref": "...", "session_id": "...", "occurred_at": "<rfc3339>" }`

## İmza kuralları (R3)

- **Signature base**: `Signature-Input`'taki bileşenler sırasıyla + `@signature-params` satırı (RFC 9421 §2.3).
- **Algoritmalar**: `ecdsa-p256-sha256` (BCL `ECDsa`, P-256) | `ed25519` (`NSec.Cryptography`).
- **Content-Digest**: SHA-256, RFC 9530 sözlük formatı; signature base'e dahil (gövde bütünlüğü).
- **Anahtar**: mağaza private key (giden imza); doğrulamada gönderen public key profil JWKS'inden `kid` ile.
- **Tazelik**: `created` zaman damgası; kabul penceresi dışı = replay reddi (doğrulama tarafı).

## Teslim

- Retry + exponential backoff (varsayılan 3-4 deneme). Teslim durumu `OutboundDelivery` izinde.
- SC-004: durum değişince ≤3 denemede teslim.

## Gelen istek doğrulama (checkout uçları)

- Aynı şema tersten: mağaza, platformun public key'ini `UCP-Agent` profil URL'inden (JWKS) çeker,
  `Signature`/`Signature-Input`/`Content-Digest`'i doğrular.
- Zorlama `UcpSigningOption.RequireSignatures` bayrağına bağlı: açık = imzasız/bozuk 401; kapalı
  (sandbox default) = varsa doğrula, yoksa geç.