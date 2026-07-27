# Data Model: IngestionAgent LLM-Sürücülü Yazıcılar (015)

**Date**: 2026-07-27 | **Plan**: [plan.md](plan.md)

Kalıcılık yok — IngestionAgent DB'siz/state'siz. Tüm tipler bellek-içi, mesaj ömürlüdür.

## Tedarikçi ürün snapshot'ı (mevcut — DEĞİŞMEZ)

- `SupplierProductSnapshotReceived` (`Shared.IntegrationEvents`): harici kimlik/SKU, ad, açıklama, fiyat, stok adedi, indirim yüzdesi (ops.), marka.
- Girdi kanoniktir (Supplier.Gateway temizler/diff'ler); LLM'e ham normalizasyon yüklenmez.

## Tipli sonuç zinciri (implement'te evrildi — RecordJob KALDIRILDI)

Paylaşılan mutable zarf yok; adımlar arasında tipli yazıcı sonuçları akar (kullanıcı kararı,
2026-07-27 refactor). Yönlendirme edge koşullarında `IsSuccess`'e bakar; sonuç workflow
output'undan okunur (`WithOutputFrom(finish)` → `WorkflowOutputEvent`).

- Akış: snapshot → `CatalogWriterResult` → `StockWriterResult` → `DiscountWriterResult` → finish.
- Başarısız sonuç hangi adımdaysa oradan doğrudan finish'e gider (FR-003); çıktı YOKSA handler
  `WORKFLOW_INCOMPLETE` fırlatır (FR-005/S4).
- Adet/indirim yüzdesi gibi girdiler executor'lara ctor'dan (mesajdan) verilir; ProductId'yi
  Catalog üretir, sonraki sonuçlara KOD yazar (Seçenek A — stok/indirim LLM şemasında yoktur).

## WriterResult ailesi (adım sonuç sözleşmesi)

Taban, stock/discount LLM'lerinin structured-output şemasıdır; adlı tipler hem catalog LLM
çıktısı (ProductId'li) hem adımlar arası mesajdır. Servis zarfının aynası DEĞİL (FR-011/FR-012).

| Tip | Alanlar | Kural |
|-----|---------|-------|
| WriterResult (taban) | IsSuccess, Error | tool çağrılmadan başarı yasak; Error'a zarf `Messages[0].Code` aktarılır |
| CatalogWriterResult | + ProductId | IsSuccess=true iken ProductId zorunlu; yoksa adım başarısız (sahte-başarı emniyeti) |
| StockWriterResult | + ProductId | executor doldurur (LLM değil); discount adımına taşınır |
| DiscountWriterResult | + ProductId | zincirin ucu; terminal çıktısı |

**Doğrulama**: deserialize hatası veya kural ihlali = adım başarısızlığı (`Failure` set edilir);
asla sessiz başarı değil (SC-002).