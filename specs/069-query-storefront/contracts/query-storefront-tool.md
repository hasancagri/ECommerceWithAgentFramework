# MCP Tool Kontratı: `query_storefront`

**Feature**: 069 | Yüzey: Storefront `/mcp` (anonim — 061 duruşu korunur). Tüketici: ChatAgent
(public + assistant allowlist'lerinin İKİSİNDE) + dış agent'lar. Silinen tool'lar:
`search_storefront_products`, `find_similar_books` (tam ikame).

**TUZAK (mevcut kural):** opsiyonel parametreye DEFAULT şart (memory `mcp-tool-optional-param-default`)
— bu tool'un tek parametresi zorunlu, tuzak tetiklenmez.

## Parametreler

| Parametre | Tip | Default | Anlam |
|---|---|---|---|
| `sql` | `string` | — (zorunlu) | `storefront_sellable` view'ına TEK SELECT/WITH sorgusu. Anlamsal metin `{{EMBED:"metin"}}` yer-tutucusuyla. |

## Response

```jsonc
{
  "Ok": true,
  "Rows": [ { "kolon_adı": "değer", ... } ],   // SELECT listesine göre serbest şekil; vector kolonları AYIKLANIR
  "RowCount": 12,                               // kırpma öncesi sayım
  "Truncated": false,                           // true → asistan "toplam büyük, daralt/sayfala" davranışına geçer
  "Error": null                                 // ret/hata: { "Code": "AgentSql...", "Detail": "tek satır makine-okur özet" }
}
```

## Bekçi sözleşmesi (çalıştırma ÖNCESİ ret — FR-004)

Sıra: `{{EMBED}}` ikamesi → bekçi → (geçerse) embedding üretimi → kısıtlı bağlantıda çalıştırma.

| Kural | Ret kodu (`StorefrontResourceConstants`) |
|---|---|
| Tek statement; `;` ile ikinci statement yasak | `AgentSqlMultiStatement` |
| Yalnız `SELECT` / `WITH` başlangıcı | `AgentSqlNotReadOnly` |
| Yasak kelime (INSERT/UPDATE/DELETE/DROP/ALTER/CREATE/GRANT/COPY/TRUNCATE/DO/EXECUTE/SET/pg_sleep/pg_read*/dblink…) — eşleşme KELİME-SINIRLI (`OFFSET` içindeki `SET` tetiklemez) ve string-literal DIŞI (`ILIKE '%drop%'` tetiklemez; literaller soyulup denetlenir) | `AgentSqlForbiddenKeyword` |
| FROM/JOIN ilişkisi yalnız `storefront_sellable` — İSTİSNA: `unnest(...)` gibi set-returning fonksiyonlar ve sorgunun kendi CTE alias'ları (WITH adları) FROM'da geçerlidir | `AgentSqlUnknownRelation` |
| SQL uzunluğu ≤ `MaxSqlLength` | `AgentSqlTooLong` |
| Geçersiz `{{EMBED}}` sözdizimi | `AgentSqlBadEmbedPlaceholder` |

Çalıştırma katmanı (bekçiyi geçse bile): kısıtlı rol → view-dışı erişim Postgres'te permission-denied
(`AgentSqlPermissionDenied`); `statement_timeout` aşımı (`AgentSqlTimeout`); diğer DB hataları
(`AgentSqlExecutionFailed`, SQLSTATE + tek satır mesajla). Her sonuç `AgentQueryLog`'a yazılır (FR-006).

## Sorgu kalıpları (prompt'un tool-tarafı sözleşmesi — ChatAgent iki prompt bloğuna girer)

- **Şema bloğu**: view kolonları + tipleri ([data-model.md](../data-model.md) tablosuyla birebir;
  drift guard `scripts/check-agent-query-schema.sh`).
- **Temalı arama**: yapısal WHERE + `embedding <=> {{EMBED:"tema"}} < 0.68` + aynı ifadeyle
  `ORDER BY` — eşik ALTI sonuç yoksa dürüst "bulunamadı" (SC-002).
- **Benzerlik**: `embedding <=> (SELECT embedding FROM storefront_sellable WHERE product_id = 'X')`
  + `product_id <> 'X'` (kendisi-hariç) + istenirse yapısal kısıt (filtreli-benzerlik, US2). Yeni
  OpenAI çağrısı gerekmez.
- **Ad eşleşmesi**: `ILIKE '%...%'` / `EXISTS (SELECT 1 FROM unnest(authors) a WHERE a ILIKE ...)` —
  yazım-varyantı dayanıklılığı (068 mirası).
- **Özellik/varyant**: `specs` jsonb `[{Attribute,Option}]` — `EXISTS (SELECT 1 FROM
  jsonb_array_elements(specs) s WHERE s->>'Attribute' ILIKE ... AND s->>'Option' ILIKE ...)`; varyant
  ailesi `family_code = (SELECT family_code FROM storefront_sellable WHERE product_id = 'X')` (eval G).
- **Sayfalama**: asistan `LIMIT/OFFSET` yazar; `Truncated=true` → toplam söyle + "devamını göstereyim
  mi" (aynı sorgu, sonraki OFFSET). Tavan 50 satır sistem garantisi.
- **Düzeltme döngüsü**: `Error.Code` gelirse asistan sorguyu düzeltip EN FAZLA 2 kez yeniden dener;
  yine olmazsa kullanıcıya dürüst "yanıtlayamadım" (FR-007, edge-case).
- **Dürüst veri sınırı**: satış adedi/bestseller verisi YOK; `added_at` yaklaşıktır — asistan bunu
  söyler, uydurmaz.
- **Grounding**: yanıt YALNIZ dönen satırlardan kurulur; boş sonuç = "bulunamadı", uydurma öneri yok.