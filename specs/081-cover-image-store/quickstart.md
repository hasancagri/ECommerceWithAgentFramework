# Quickstart / Doğrulama: Kapak Görseli Deposu (File.Api)

Aspire AppHost'tan başlat: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
(File.Api + gateway aynı grafikte). Migration config'i: `CoverMigration:Enabled=true` +
`SourceXlsxPath` = `~/dev/catalog-data/catalog-import.xlsx`; `CoverStore:RootPath` = kalıcı host dizini.

## Senaryo 1 — Kapak stabil URL'den servis (US1)

1. Depoda kapağı olan bir ISBN seç (migration sonrası).
2. `GET {gateway}/files/v1/covers/{isbn}`.
3. **Beklenen**: 200 + görsel + doğru `Content-Type`. Olmayan ISBN → 404. `..`/`/` içeren ISBN → 400.

## Senaryo 2 — Migration bir-kez besleme (US2)

1. Boş `CoverStore:RootPath` + `Enabled=true` ile başlat.
2. **Beklenen**: File.Api log'unda özet `{yazıldı, atlandı, başarısız}`; erişilebilir görseller
   `{root}/covers/{isbn}` (+ `.ct`) olarak yazılır. Bozuk `imageUrl` satırı süreci durdurmaz (başarısız sayılır).

## Senaryo 3 — Kalıcılık + idempotent re-run (US3)

1. Migration bitince AppHost'u durdur/yeniden başlat.
2. **Beklenen**: kapaklar diskte durur → Senaryo 1 yeniden indirmeden çalışır (SC-001).
3. Migration'ı tekrar çalıştır → log: var olanlar **atlandı**, yalnız eksikler yazıldı (SC-004).

## Domain testleri (İLKE VI — test-first)

- `CoverKey`: geçerli isbn → güvenli key; `/`, `\`, `..`, boşluk → reddedilir (path-traversal guard).
- Migration skip kararı: `Exists(isbn)` true → atla; false + imageUrl dolu → indir; imageUrl boş → atla.

## Guard / Notlar

- Kapak okuma anonim (gateway `/files/**` policy'siz).
- `Product.ImageUrl` rewrite YOK (sonraki Excel import). Bu feature yalnız depo + serve + migration.