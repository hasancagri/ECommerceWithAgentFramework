# Research: Tek Müşteri MCP Fasadı

Phase 0 — teknik bilinmeyenlerin çözümü. Kaynak: mevcut kod (ChatAgent MCP-client deseni, 061 OAuth/PRM,
057 anonim sepet, 070 dış-admin agent), ModelContextProtocol .NET SDK, canlı mcp-remote davranışı.

## R1 — Fasad = dinamik MCP server (aggregation mekanizması)

- **Karar**: `Mcp.Gateway`, `ModelContextProtocol.AspNetCore` ile MCP **server** açar; tool listesi
  **statik değil, oturum anında dinamik** üretilir. `ListTools` isteğinde fasad alt BC'lere MCP client
  (`McpClient` + `HttpClientTransport`) ile bağlanıp tool'ları toplar; `CallTool` isteğinde adı registry'den
  çözüp sahibi BC'ye proxy'ler. Server tarafı dinamik tool sağlayıcı (`ConfigureSessionOptions` /
  tool-collection mutasyonu — 070 customer `ConfigureSessionOptions` emsali) ile yüzeye göre budanır.
- **Gerekçe**: ChatAgent zaten uzak MCP tool'unu client olarak sarıyor (`PerUserMcpTool`); server tarafı
  ikizi aynı transport'u kullanır. SDK oturum-başına tool koleksiyonu mutasyonunu destekliyor (070'te
  /mcp vs /mcp-admin budaması bununla yapıldı).
- **Alternatif**: Statik tool tanımı (build-time) — reddedildi: BC tool'ları değişince fasad bayatlar +
  069 startup-snapshot tuzağı. Dinamik/lazy tercih edildi.
- **RİSK (spike)**: SDK'nın server-tarafı "tool listesini downstream'den runtime türet" akışı; plan
  implementasyonunda ilk iş küçük bir spike (tek BC ile ListTools+CallTool proxy uçtan uca).

## R2 — Anonim bağlan + checkout'ta step-up tek login

- **Karar**: `/mcp` ucu **blanket-auth DEĞİL**: bağlanma + `ListTools` + anonim (okuma/anonim-sepet)
  tool'lar girişsiz. Korumalı (checkout/sipariş/ödeme/hesap) tool ilk çağrıldığında fasad **401 +
  RFC 9728 challenge** döner → istemci (mcp-remote) OAuth dansını **o an** yapar (`handleOAuthUnauthorized`)
  → tek giriş → sonraki çağrılar token'lı. Yani step-up, satın almada bir kez.
- **Gerekçe**: E-ticaret normu (anonim gez, satın alırken gir). mcp-remote oturum-ortası 401'de auth
  tetikliyor (canlı gözlem). Tek consent superset scope ile tek login.
- **Alternatif**: Upfront blanket-auth (bağlanınca login) — reddedildi (kullanıcı itirazı; e-ticaret
  normuna aykırı). İki ayrı uç (anonim /mcp-public + korumalı /mcp) — reddedildi (tek-MCP değerini böler).
- **RİSK (spike)**: mcp-remote sürümüne göre oturum-ortası step-up davranışı; [[mcp-remote-issuer-slash-gotcha]]
  sürüm katılığı dersi — spike'ta çalışan sürüm doğrulanır/pinlenir.

## R3 — Anonim sepet + login-devir (agent yoluna açma)

- **Karar**: Girişsiz sepet, fasadın oturuma/cihaza bağlı ürettiği **anonim kimlikle** (X-User-Key yan
  yolu, 061/057) Basket'e taşınır. Checkout step-up login anında fasad, anonim sepeti kullanıcıya
  **devreder** (Basket domain'deki mevcut `MergeFrom` yeteneği; 057). Devir sonrası oturum kullanıcı
  token'ıyla sürer.
- **Gerekçe**: Anonim sepet (057) + login-merge domain'de VAR; yüzeyi söküktü ([[anonymous-basket-chat-gap]]).
  Bu feature merge'i agent/fasad yoluna açar.
- **Alternatif**: Girişte-sepet (add-to-cart login ister) — kullanıcı reddetti (option 2 seçildi: ödemede
  giriş). Anonim sepeti hiç desteklememek — e-ticaret normuna aykırı.
- **RİSK**: En ağır alt-iş. Anonim X-User-Key yaşam döngüsü (fasad üretir/taşır) + merge tetikleme
  (login anı). Basket'in anonim + merge yüzeyinin agent yolundaki 4-katman bloğu bu feature'da çözülür.

## R4 — Token modeli (discovery makine, çağrı kullanıcı)

- **Karar**: **ListTools** (keşif) fasadın makine kimliğiyle (`mcp-gateway-discovery` client_credentials,
  salt audience üretimi) yapılır — ChatAgent keşif token'ı emsali (061). **CallTool** her zaman o anki
  **kullanıcı bearer'ıyla** (anonim aşamada X-User-Key) sahibi BC'ye taşınır. Fasad kullanıcı token'ını
  KALICI saklamaz.
- **Gerekçe**: Keşif kullanıcı-bağımsız (tool şeması herkese aynı); çağrı kullanıcı-yetkili. 061 ayrımı
  birebir (discovery m2m, invoke per-user).
- **Alternatif**: Keşifte de kullanıcı token'ı — reddedildi (anonim aşamada token yok; keşif girişten önce olmalı).

## R5 — Tek consent superset scope (external-customer-agent)

- **Karar**: Yeni seed OAuth istemci `external-customer-agent` (public+PKCE, `ConsentType=Explicit`,
  müşteri scope demeti: basket.read/write, order.read/write, customer.read, payment.read, storefront.read).
  Tek consent = tek login. Yönetim fasadı mevcut `external-admin-agent` (070) kullanır.
- **Gerekçe**: 070/061 dış-agent seed deseni hazır (loopback + Claude callback redirect, DcrRequestValidator
  dışı). Superset scope tek consent'te toplanır; gerçek yetki = scope ∩ kullanıcı rolü (030).
- **Alternatif**: DCR (dinamik kayıt) — gereksiz; sabit seed istemci yeter. client_credentials — hayır
  (per-user kimlik gerek).

## R6 — Yüzey ayrımı + tool-adı benzersizliği

- **Karar**: `/mcp` (müşteri) ve `/mcp-admin` (admin) iki PRM + iki tool kümesi (070 `ConfigureSessionOptions`
  yol-prefix budaması emsali). Tool adları mağaza genelinde benzersiz (`Shared/McpToolNames`) → namespace
  YOK; registry ad→BC+yüzey eşler.
- **Gerekçe**: 070 zaten tek serviste /mcp + /mcp-admin yüzey budamasını çözdü; fasad çok-servis toplamda
  aynı deseni uygular. Benzersiz adlar routing'i sadeleştirir.
- **Alternatif**: BC-prefix namespace (`basket.add_to_cart`) — reddedildi (adlar zaten benzersiz; prefix
  LLM tool seçimini bozar, mevcut McpToolNames'i kırar).

## R7 — Fasadın konumu (servis mi, gateway mi)

- **Karar**: Ayrı izole servis `src/agents/Mcp.Gateway` (DB'siz standalone MCP; Mail.Mcp emsali). Gateway
  yalnız route eder (saf reverse-proxy doğası korunur).
- **Gerekçe**: Toplama+proxy mantığı YARP gateway'e sızmamalı (gateway saf kalsın). İzole servis test
  edilebilir + İlke I ruhuna yakın.
- **Alternatif**: Gateway içinde MCP server — reddedildi (gateway'e uygulama mantığı sızdırır).

## R8 — SPIKE sonucu (T010) + auth fallback kararı

- **SDK fizibilitesi: GREEN.** ModelContextProtocol 1.4.0 `WithListToolsHandler` + `WithCallToolHandler`
  (server custom handler → dinamik proxy) + client `McpClient.CreateAsync`/`HttpClientTransport` mevcut.
  Fasad çekirdeği (lazy toplama + ad→BC yönlendirme + token-forward) uygulanabilir (dll doğrulandı).
- **Auth zamanlaması — fallback (kullanıcı kararı):** step-up (anonim→checkout'ta giriş) canlıda tutmazsa
  **upfront login**. Bu yüzden fasad `RequireLoginUpfront` bayraklı:
  - `true` (garantili taban): `/mcp` blanket-auth → bağlanınca tek login; anonim/anon-sepet YOK → merge
    gerekmez. Kanıtlanmış mekanizma.
  - `false` (iyileştirme): anonim gez+sepet + checkout step-up + login-merge (R2/R3). Canlı step-up
    tutarsa açılır; tutmazsa `true` kalır.
- **Gerekçe**: Kullanıcı "olmuyorsa baştan login, yapacak bir şey yok" dedi. Bayrak, en riskli iki
  parçayı (mid-session step-up + anon-basket-merge) opsiyonele indirir; MVP her hâlükârda çalışır.

## Açık riskler (özet)

- **Canlı step-up** (mcp-remote mid-session 401): `RequireLoginUpfront=false` yolunun canlı doğrulaması;
  tutmazsa bayrak `true` (fallback).
- **En ağır alt-iş** (yalnız `false` yolunda): anonim sepet X-User-Key + login-merge agent yoluna açma (R3).