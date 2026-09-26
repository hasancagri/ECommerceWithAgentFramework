# Quickstart: 085 Tek MCP Yüzeyi — Canlı Doğrulama

Ön koşul: AgentPlatform R3 PR'ı merge + IdP ayakta; sistem `dotnet run --project src/aspire/AppHost/AppHost.csproj`. Eski `~/.mcp-auth` kayıtları + tarayıcı IdP cookie'si temiz (kullanıcı-geçiş tuzağı).

## S1 — Admin tek uçtan her şeyi görür (US1)

1. Claude Desktop `magaza-admin` kaydı: `mcp-remote <fasat>/mcp?client=admin` → OAuth'ta ADMIN kullanıcıyla login.
2. Beklenen: tool listesinde müşteri seti + TÜM admin tool'lar (keşif anındaki `*AdminSurface` holder içerikleri; 2026-09-26 itibarıyla 33: catalog 22, stock 3, customer 5, discount 3).
3. Bir admin yazma tool'u çağır (ör. `admin_adjust_stock`) → başarı + ilgili BC'de `AdminActionLog` satırı.
4. `curl -s -o /dev/null -w "%{http_code}" <fasat>/mcp-admin` → **404**. Aynısı BC'lerde ve gateway `/mcp-admin/{service}` rotalarında.

## S2 — Müşteri admin tool'u göremez/çağıramaz (US2)

1. `magaza` kaydı: `mcp-remote <fasat>/mcp` → TEST müşterisiyle login (union PRM'den fazla scope talep edilir — bağlantı KIRILMAMALI, token müşteri demetiyle basılır).
2. Beklenen: listede admin tool sayısı **0**; `query_storefront`/sepet akışı bugünkü gibi.
3. Ham JSON-RPC ile `admin_set_published` adını doğrudan `tools/call` et → tool-error (yetki), işlem YOK (ürün yayın durumu değişmedi).
4. Anonim BC kontrolü: `catalog-api/mcp`'ye token'sız `tools/list` → yalnız anonim keşif seti (070 seti birebir).
5. Discount kontrolü (FR-001a): `discount-api/mcp`'ye token'sız istek → 401 (uç korumalı); admin token'sız oturumda discount tool'u listelenmez.

## S3 — Yan yana iki kimlik (US3)

1. İki kayıt aynı anda bağlı (S1 admin + S2 test müşterisi).
2. Sohbette önce sepete ürün ekletip sonra stok düzelttir → sepet test müşterisinin, `AdminActionLog` admin kullanıcının kimliğinde. Karışma 0.
3. `~/.mcp-auth/mcp-remote-v1/` altında İKİ ayrı hash dosyası doğrula (`?client=admin` ayrıştırması).

## S4 — Kısmi admin scope (edge)

1. IdP `Pages/Admin`: `catalog-manager` benzeri geçici rol — demeti YALNIZ `catalog.read`+`catalog.write`; test kullanıcısına ata.
2. O kullanıcıyla `/mcp?client=cm` kaydından bağlan → listede catalog admin tool'ları VAR, stock/customer/discount admin tool'ları YOK.
3. `admin_set_stock` adıyla doğrudan çağrı → tool-error (scope yok).

## S5 — DCR tavanı (FR-005 / SC-003)

1. DCR ile yeni istemci kaydet (mcp-remote'un doğal akışı, `~/.mcp-auth` temizken herhangi bir üçüncü kayıt).
2. Admin KULLANICIYLA login olunsa bile: token'da admin scope'u YOK (ScopeResolver `clientCeiling` kesişimi — `ExternalAgentDefaults`).
3. Admin tool çağrısı → tool-error.

## Regresyon hızlı turu

- `dotnet build` + `dotnet test` yeşil (SurfaceFilterTests silindi, ScopePruning testleri geçti; bağımlı TEST projeleri de derlendi — rename tuzağı).
- Müşteri E2E: arama → sepet → `start_payment` → hosted ödeme → sipariş Confirmed (077 akışı birebir).
- `scripts/check-flow-links.sh` yeşil (FLOW.md anchor'ları; mağaza BC süreçleri değişmedi).
