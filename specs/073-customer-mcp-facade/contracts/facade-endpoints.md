# Contract: Fasad Uçları + Tek-Login / Step-Up

Fasad iki MCP uç sunar; gateway tek dış girişten yayınlar. Müşteri/yönetim yüzeyleri ayrık.

## Uçlar

- `POST /mcp` — müşteri fasadı (MCP streamable HTTP transport). Yüzey = `customer`.
- `POST /mcp-admin` — yönetim fasadı. Yüzey = `admin`.
- `GET /.well-known/oauth-protected-resource/mcp` — müşteri PRM (RFC 9728; müşteri scope demeti).
- `GET /.well-known/oauth-protected-resource/mcp-admin` — yönetim PRM (admin scope demeti).

(`McpResourceMetadataExtension` zaten /mcp + /mcp-admin destekli — 070.)

## Auth modeli (anonim + step-up)

- `/mcp` **blanket-auth DEĞİL**. Bağlanma + `ListTools` + `RequiresUserAuth=false` (anonim: katalog/vitrin/
  anonim-sepet) tool çağrıları **girişsiz** çalışır.
- İlk **korumalı** (`RequiresUserAuth=true`: checkout/sipariş/ödeme/hesap) tool çağrısında fasad
  **401 + `WWW-Authenticate: Bearer resource_metadata=..., scope="<müşteri demeti>"`** döner → istemci
  OAuth dansını o an yapar (step-up) → tek consent → token'lı retry.
- Tek giriş sonrası oturumda ikinci giriş İSTENMEZ. Alt BC'lerin kendi 401/consent'i istemciye ULAŞMAZ
  (fasad token'ı forward eder; downstream challenge fasada döner, müşteriye değil).

## Yüzey ayrımı

- `/mcp` yalnız `customer` yüzey tool'larını listeler/çalıştırır; `/mcp-admin` yalnız `admin`. Çapraz
  görünürlük YOK (SC-006). Yanlış yüzeyde tool çağrısı → bulunamadı hatası.

## Anonim sepet → login devri

- Girişsiz sepet fasadın ürettiği opak `UserKey` ile taşınır (X-User-Key).
- Checkout step-up login tamamlanınca fasad, anonim sepeti kullanıcıya devreder (Basket merge; 057) →
  kalemler korunur (SC-003).

## Hata semantiği

- Kayıtta olmayan tool → MCP tool-error (açıklayıcı), oturum düşmez.
- Alt BC yetki reddi → red içerik olarak iletilir; diğer tool'lar etkilenmez.
- Fasad hiçbir alt-BC hatasında çökmez.