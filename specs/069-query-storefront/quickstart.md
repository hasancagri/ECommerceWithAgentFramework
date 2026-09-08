# Quickstart: Query Storefront (canlı doğrulama)

**Feature**: 069 | Kontrat: [contracts/query-storefront-tool.md](contracts/query-storefront-tool.md) |
Eval: [contracts/eval-set.md](contracts/eval-set.md) | Model: [data-model.md](data-model.md)

## Ön koşullar

1. OpenAI anahtarları (Storefront `{{EMBED}}` + ChatAgent) — mevcut fail-fast düzeni:
   ```bash
   dotnet user-secrets set OpenAI:ApiKey <key> --project src/services/storefront/Storefront.Api/Storefront.Api.csproj
   ```
2. Kısıtlı rol şifresi (yeni):
   ```bash
   dotnet user-secrets set AgentQueryOption:RolePassword <pwd> --project src/services/storefront/Storefront.Api/Storefront.Api.csproj
   ```
3. Sistem her zaman Aspire'dan:
   ```bash
   dotnet run --project src/aspire/AppHost/AppHost.csproj
   ```

## Q1 — Bootstrap + yapısal zemin (US3 önkoşulu)

Açılış sonrası storefrontDb'de:

```sql
select count(*) from storefrontmanagement.storefront_sellable;               -- ~yayındaki kitap sayısı
set role <RoleName>; select 1 from storefrontmanagement.mt_doc_userpurchase; -- BEKLENEN: permission denied
```

- View var, satır sayısı yayındaki ürünle tutarlı; `IsDeleted` ürün view'da YOK (SC-004).
- Kısıtlı rol view DIŞINDA hiçbir tabloyu okuyamaz (SC-003 yapısal kanıt).
- İkinci restart: bootstrap idempotent (hata yok, `CREATE OR REPLACE` sessiz geçer).

## Q2 — Güvenlik probları (US3, SC-003) — chat + doğrudan MCP

Chat'e/MCP'ye sırayla: veri değiştirme niyeti ("ucuzlat şu kitabı" → asistan reddeder ya da sorgu
`AgentSqlNotReadOnly`), çoklu-statement (`...; DROP TABLE ...`), view-dışı ilişki
(`mt_doc_userpurchase`), devasa sonuç (LIMIT'siz tüm katalog), uzun-süren sorgu.

- Beklenen: HİÇBİRİ çalışmaz — bekçi kodlu ret ya da rol permission-denied; tavan/timeout kırpar.
- Her deneme `AgentQueryLog`'da `Rejected/Failed` satırıyla izli:
  ```sql
  select data->>'Verdict', data->>'RejectCode', data->>'Sql'
  from storefrontmanagement.mt_doc_agentquerylog order by data->>'CreatedAt' desc limit 10;
  ```

## Q3 — 067 gerileme koruması (US2, SC-002)

067 quickstart S3 + S5 senaryoları yeni kapıda tekrarlanır:

1. "Kışın okunacak sürükleyici bilim kurgu, 300 TL altı, X yayınevi hariç" → her sonuç kısıtlara
   uyar; log'daki SQL'de `{{EMBED}}` + eşik 0.68 deseni görünür.
2. "Traktör motoru rektifiye el kitabı" → dürüst "bulunamadı"; uydurma liste YOK.
3. Bir kitaba "buna benzer ne var" → kendisi-hariç benzerler; YENİ: "benzer ama 200 TL altı ve
   stokta" → benzerlik + kısıt birlikte (US2 yeni kazanım).
4. Sonuçtaki bir kitabı admin'den yayından kaldır → aynı sorgu tekrarında kitap dönmez (view filtresi).

## Q4 — Yeni soru sınıfları (US1, SC-001 ön-tur)

Chat'e: "kategori başına ortalama fiyat" · "Tolkien mi King mi daha çok kitaba sahip" · "fantastik
kategorisinden veya Le Guin'den bir şeyler" · "en çok kitabı olan yazar".

- Beklenen: gerçek veriden hesaplanmış yanıtlar (SQL çapraz-doğrulaması `AgentQueryLog`'daki sorguyla);
  "buna sığmıyor" reddi YOK.

## Q5 — Eval koşumu (FR-008, SC-001)

[contracts/eval-set.md](contracts/eval-set.md) A–M sınıfları chat'ten koşulur; sınıf başına
geçti/kaldı işaretlenir. Geçme çıtası: ≥ %90 (J muafiyetiyle). Her yanıtın arkasındaki sorgu
`AgentQueryLog`'dan bulunur (SC-005 kanıtı).

## Q6 — Birim testleri + guard'lar

```bash
dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj  # AgentSqlGuard + EmbedPlaceholder + LimitWrap (test-first)
dotnet build                                                         # tüm çözüm (silinen tool referansları temiz)
scripts/check-agent-query-schema.sh                                  # YENİ: view şeması ↔ ChatAgent prompt drift guard'ı
scripts/check-flow-links.sh                                          # FLOW.md anchor guard (İLKE VII — güncellenen süreç)
scripts/check-claude-spec-links.sh                                   # BC haritası spec yolları
```

- Beklenen: hepsi yeşil; FLOW.md'de "tek sorgu kapısı" adımı anchor'larıyla mevcut; CLAUDE.md
  storefront satırı yeni yüzeyi anlatıyor.