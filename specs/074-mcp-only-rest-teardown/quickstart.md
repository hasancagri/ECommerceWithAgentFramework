# Quickstart — 074 Statik Doğrulama

**Canlı test YOK** (kullanıcı kararı). Doğrulama: `grep` + `dotnet build` + kod incelemesi.
Amaç: domain iş REST'i tümüyle kalktı, catalog admin parite kuruldu, korunan yollar kodda ayakta.

## Ön koşul

```bash
dotnet build     # 0 hata (söküm + parite sonrası)
```

## S1 — Söküm doğrulaması (grep; SC-001/006/007)

```bash
# Domain iş REST endpoint'i kalmamalı (auth/MCP-infra/S2S/gRPC hariç):
grep -rn --include=*.cs -E "\.Map(Get|Post|Put|Delete|Patch)\(" src/services/catalog src/services/stock \
  src/services/checkout | grep -viE "internal/|MapMcp|ResourceMetadata"          # → boş
# customer'da yalnız /internal/* kalmalı:
grep -rn "MapGroup\|MapGet\|MapPost" src/services/customer/Customer.Api/Domains | grep -v internal   # → boş
# .http yok:
find . -name "*.http" -not -path "*/bin/*" -not -path "*/obj/*"                   # → boş
# gateway REST proxy yok:
grep -c "catalog-route" src/services/gateway/Gateway/appsettings.Development.json # → 0
# kullanıcı/admin Command|Queries kalmadı (yalnız kritik/iç):
find src/services -path "*/Features/Commands/*.cs" -o -path "*/Features/Queries/*.cs" | grep -v otherProjects
#   → yalnız: ClearBasketByCheckout, GetBasket, CommitStock, RevertCommitStock, GetMerchantKeyInternal, ImportBook
```

## S2 — Anonim `/mcp` keşif seti değişmedi (kod incelemesi; SC-005)

```bash
# Yeni admin tool'lar YALNIZ /mcp-admin — anonim /mcp keşif setine sızmamalı.
# Program.cs ConfigureSessionOptions yol-prefix filtresi DEĞİŞMEMİŞ olmalı:
grep -n "ConfigureSessionOptions\|StartsWithSegments(\"/mcp-admin\")" src/services/catalog/Catalog.Api/Program.cs
```

İncele: anonim catalog `/mcp` tool seti = `get_product`, `search_products`, `get_price_history`,
`list_categories`, `list_authors`, `list_publishers` (yeni `admin_*` bunlarda YOK; `[McpServerTool]`
attribute'lu admin sınıfları filtreyle yalnız `/mcp-admin`).

## S3 — Catalog admin parite kuruldu (kod incelemesi; SC-002/004)

```bash
# 15 yeni Agent slice + wrapper var mı:
find src/services/catalog -path "*/Features/Agents/Admin*ForAgent.cs" | sort
grep -rn "CatalogAdminTools\." src/services/catalog/Catalog.Api/Domains/*/["'"'"']*McpTools.cs" 2>/dev/null
```

İncele: her yeni yazma tool'u `[RequiredScope(CatalogWrite)]` + `AdminActionLog.Executed/Rejected`
(`AdminUpdateProductForAgent` deseni); `admin_create_product` ISBN çakışması→Error (R1); wrapper ince
sarmalayıcı (`IMessageBus.InvokeAsync`, `ICurrentUser`→UserId); opsiyonel param `= null`.

## S4 — Korunan yollar kodda ayakta (kod incelemesi; SC-003)

```bash
# S2S + gRPC + saga + auth + MCP-infra DOKUNULMAMIŞ:
grep -rn "internal/payment-context\|internal/merchant-key" src/services/customer   # → var
ls src/others/Shared/Protos/basket_items.proto src/others/Shared/Protos/basket_clear.proto  # → var
grep -rn "CommitStock\|RevertCommitStock" src/services/stock/Stock.Api/StockEventHandlers.cs # → var
grep -rn "ReconcileTick" src/services/order/Order.Api/Process/                     # → var
grep -rn "MapMcp\|MapMcpResourceMetadata" src/agents/Mcp.Gateway/Program.cs        # → var
```

## S5 — Derleme (regresyon yok kanıtı; SC-003)

```bash
dotnet build     # tüm çözüm 0 hata; ölü referans/GlobalUsings yok
```

Tümü beklenen sonucu verirse 074 kabul kriteri (statik) karşılanır. Canlı E2E kapsam dışı.