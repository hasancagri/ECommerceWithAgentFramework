# Contract: Discovery (ListTools) + Proxy (CallTool)

Fasadın çekirdek davranışı: lazy toplama + token-forward yönlendirme.

## ListTools (lazy keşif)

1. İstemci `/mcp` (veya `/mcp-admin`) oturumunda `ListTools` çağırır.
2. Fasad, yüzeye uyan downstream'lere (`Surface` eşleşen) paralel bağlanır (MCP client), her birinde
   `ListTools` çağırır — **makine token'ıyla** (`mcp-gateway-discovery` client_credentials).
3. Toplanan şemalar birleşir → yüzeye göre süzülür → `ToolRoutingRegistry` kurulur/güncellenir.
4. Sonuç kısa-TTL cache'lenebilir (`CacheTtlSeconds`); **startup-snapshot YOK** (069 tuzağı).
5. Bir downstream erişilemez → o BC atlanır (graceful degrade, log); sonraki ListTools'ta döner.

**Not**: Keşif kullanıcı-bağımsızdır (şema herkese aynı) → anonim istemci de tam listeyi görür; giriş
yalnız korumalı tool ÇAĞRISINDA (step-up).

## CallTool (token-forward proxy)

1. İstemci `CallTool(name, args)` çağırır.
2. Fasad `Resolve(name)` → sahibi BC + yüzey. Bulunamazsa → MCP tool-error (reddedilmez sessizce).
3. Yüzey uyuşmazsa (ör. admin tool /mcp'de) → bulunamadı.
4. `RequiresUserAuth=true` ve kullanıcı token'ı yoksa → **401 + PRM challenge** (step-up login tetikler).
5. Sahibi BC `/mcp`'ye yeni MCP client oturumu açılır; **kullanıcı bearer'ı** (anonim aşamada anonim
   `UserKey`/X-User-Key) forward edilir; tool çağrılır; sonuç aynen döndürülür.
6. Fasad kullanıcı token'ını KALICI saklamaz (çağrı başına taşır). ChatAgent `PerUserMcpTool` server ikizi.

## Checkout step-up + sepet devri

- İlk korumalı checkout tool çağrısı 401 → login → token.
- Login anında fasad anonim `UserKey` sepetini kullanıcıya devreder (Basket merge, 057) → kalemler korunur.

## Hata

- Downstream 401 (kullanıcı yetkisi yetersiz) → red içerik istemciye iletilir; oturum düşmez.
- Downstream erişilemez (CallTool) → MCP tool-error ("şu an erişilemiyor"); diğer tool'lar çalışır.