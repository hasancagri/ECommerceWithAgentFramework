# Kontrat: Supplier Feed `familyCode` Alanı (045)

`GET /v1/feeds/{kod}` satır şeması — ADDITIVE; eski rev dosyaları/okuyucular kırılmaz.

## Alan

```json
{ "barcode": "8690000000101", "name": "Peak Kulaklık 1 Kırmızı", "...": "...",
  "attributes": { "Renk": "Kırmızı" },
  "familyCode": "PEAK-KLK-1" }
```

| Alan | Tip | Kural |
|---|---|---|
| familyCode | string? | OPSİYONEL; boş/whitespace = yok sayılır (null). Tedarikçi-içi ve tedarikçiler-arası aynı model için AYNI kod (sözleşme varsayımı). |

- Aile üyeliği satır seviyesindedir; her üye kendi barkoduyla ayrı satırdır (kombinasyon üretimi YOK).
- Çakışma (aynı barkoda farklı kod): alan-bazlı Priority-merge karar verir (düşük Priority dolu değer kazanır).
- Mock dataset güncellemesi ELLE (kod-içi üretici yasak — repo kuralı): rev dosyalarına örnekler:
  a) 3 üyeli aile (yalnız Renk ayrışır), b) 2 eksenli aile (Renk+Beden benzeri), c) tek üyeli aile,
  d) kodsuz satırlar (mevcut çoğunluk), e) supplier-a/b çakışması (aynı barkod farklı kod),
  f) bir rev'de kodun KALDIRILDIĞI üye (SC-005 canlı senaryosu).
