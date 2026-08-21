# Quickstart: Ürün Özellikleri ve Facet Filtre (043) — Canlı Doğrulama

Uçtan uca kanıt: feed attributes → Procurement merge/enrich → Catalog atama → Storefront facet →
WebApp filtre + detay tablosu.

## Önkoşullar

- Sistem Aspire'dan ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Mock rev dosyalarında `attributes` örnekleri mevcut (implement sırasında eklenir):
  bazı satırlar tam, bazıları kısmi, bazıları alansız (enrich yolunu test için).

## Adımlar

1. **Seed doğrula** — Catalog `GET /api/v1/specification-attributes` (List ucu): 4 attribute +
   option'ları dönüyor. pgAdmin: Procurement seed tabloları/registry dolu.

2. **Feed çekimi** — `POST /v1/feeds/pull` (Procurement dev tetiği). PoolProduct listing'lerinde
   `RawAttributes` dolu; kanonik içerikte eşlenmiş Specs görünüyor.

3. **Öncelik birleşimi** — iki tedarikçinin AYNI barkodda farklı attribute verdiği üründe:
   düşük Priority'nin dolu değeri kazanmış; tek tarafın verdiği attribute kaybolmamış.

4. **Enrich yolu** — attributes'sız üründe enrich koştuktan sonra (kuyruk/manuel bekleme) AI'ın
   kapalı listeden seçtiği spec kanonikte; liste-dışı hiçbir değer yok (DB'de registry-dışı ad
   araması boş döner). Spec'siz kalan ürün de YAYINDA (Status etkilenmemiş).

5. **Catalog + Storefront** — ürün upsert'i sonrası: Product.Specifications atamaları dolu;
   `GET /filters` yanıtında `specifications` bölümü + doğru `count` değerleri.

6. **WebApp filtre (US1)** — /Products sol panelde spec checkbox'ları:
   - "Renk: Siyah" seç → liste yalnız siyahlar; URL `spec=Renk|Siyah` taşıyor; yenile → korunur.
   - "Renk: Beyaz" ekle → liste genişler (OR). "Materyal: Çelik" ekle → daralır (AND).
   - Kategori filtresi + spec + sayfalama birlikte; "Temizle" hepsini sıfırlar.
   - Facet'teki count = filtre uygulanınca dönen ürün sayısı (birebir, SC-006).

7. **Detay tablosu (US2)** — özellikli ürün detayında "Özellikler" tablosu sıralı; özelliksiz
   üründe bölüm yok.

8. **Regresyon** — özelliksiz ürünler listede normal (filtre seçili değilken); yayın sayısı feed
   öncesiyle aynı (SC-005); 042 davranış logu satırları akmaya devam ediyor.

## Beklenen sonuç özeti

| Kontrol | Beklenen |
|---------|----------|
| Registry seed | İki BC'de aynı adlar; List ucu döner |
| Merge | Attribute-başına, sıra-bağımsız, öncelik kazanır |
| Enrich | Yalnız kapalı listeden; liste-dışı %0; spec'siz yayın sürer |
| Facet | count birebir; veri yokken bölüm gizli |
| Filtre | Grup içi OR, gruplar arası AND; URL taşınabilir |
| Detay | Sıralı tablo; boşsa gizli |

## Testler

```bash
dotnet test tests/Procurement.Api.Tests/Procurement.Api.Tests.csproj   # merge + guard
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj           # aggregate + atama
dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj     # ApplyFilters + facet
```
