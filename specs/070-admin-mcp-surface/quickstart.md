# Quickstart: 070 Admin MCP Surface — Canlı Doğrulama

Ön koşul: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (tüm sistem), OpenAI user-secrets
(CLAUDE.md), Claude Desktop kurulu, Identity.Server bootstrap admin hesabı bilinir.

## 1. Guard'lar + build

```bash
dotnet build && dotnet test
scripts/check-agent-query-schema.sh     # yeni hedef: StorefrontMcpTools.cs — OK beklenir
scripts/check-claude-spec-links.sh
```

## 2. Admin OAuth akışı (US1 ön şartı)

1. Claude Desktop → Settings → Connectors → `https://localhost:<gw>/mcp-admin/catalog` ekle.
2. Beklenen: 401 challenge → tarayıcıda Identity.Server login (admin hesabı) → consent EKRANSIZ
   (Implicit) → bağlantı yeşil, 5 catalog admin tool listelenir.
3. Negatif: aynı bağlantıyı `customer` rollü hesapla yap → tool çağrısı 403 (SC-002).
4. Negatif: DCR'lı normal istemciyle `catalog.write` iste → token'da scope YOK (SC-006).

## 3. US1 — ürün yönetimi zinciri (Claude Desktop sohbeti)

"Ürünleri listele" → sayfalı liste; "X'in detayını aç" → tam künye; "fiyatını 95 yap" →
güncel hâl döner; "yayından kaldır" → vitrinde `query_storefront` ile ARANMAZ olduğunu doğrula;
"fiyat geçmişini göster" → 120→95 kaydı görünür.
DB izi: catalogDb `AdminActionLog` — update + publish kayıtları var, Summary'de sır yok.

## 4. US2 — stok

"Stoğu kaç? 25 yap. 3 azalt." → 25 → 22; "-30 azalt" → negatif reddi iş hatası. stockDb AdminActionLog izi.

## 5. US3 — merchant kimlik + onboarding sarmalayıcı

`/mcp-admin/customer` bağla. "Merchant kimliği tanımlı mı?" → maskeli durum (key YOK).
"Şu Id/Key ile kaydet" → configured:true; ardından chat'ten sipariş çekimi yeni key'le çalışır
(PlaceOrder canlı akışı). Onboarding: "gateway'e başvur" → PG Pending; "durum?" → status döner.
PG kapalıyken: "şu an yapılamıyor" (teknik detay sızmaz).

## 6. US4 — taksit (müşteri yolu)

Claude Desktop MÜŞTERİ hesabıyla `/mcp/order` bağlı: sepete ürün ekle → "kayıtlı kartımla taksitler?"
→ seçenek listesi; ChatAgent chat'indeki (hâlâ canlı) kural-8 sonuçlarıyla AYNI (SC-005).
Boş sepetle sor → yönlendirici hata.

## 7. US5 — playbook göçü (temiz oturum)

ChatAgent'a HİÇ girmeyen temiz Claude Desktop oturumu, `/mcp/storefront` bağlı:
- "kış temalı sürükleyici bilim kurgu öner" → {{EMBED}} kalıbı + eşik doğru kullanılır, alakalı sonuç.
- "Dune'a benzer kitaplar" → alt-sorgu benzerlik kalıbı.
- "tüm fantasy kitaplarını listele" → önce COUNT + ilk sayfa + "devam?" davranışı.
- 069 eval setinin dış-agent muadelesi: 13 senaryo → hedef 13/13 (SC-004).
- Regresyon: WebApp chat'inde (hâlâ canlı) aynı sorular → davranış bozulmadı (FR-013).

## 8. Kapanış kontrolü

- SC-001: 058 ekranındaki her işlem yalnız Claude Desktop'tan yapıldı (madde 3-5).
- Anonim `/mcp/{catalog|stock|storefront}` uçları hâlâ token'sız çalışıyor (regresyon).
- `dotnet test` yeşil; guard script'leri yeşil.
