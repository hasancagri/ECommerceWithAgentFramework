# Quickstart: Ürün Varyantları (045) — Canlı Doğrulama

Uçtan uca kanıt: feed familyCode → Procurement merge/hash → Catalog → Storefront gruplama →
WebApp tek kart + detay seçici. Kontratlar: contracts/ altında.

## Önkoşullar

- Sistem Aspire'dan ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Mock rev dosyalarında familyCode örnekleri (implement'te elle eklenir):
  3-üyeli Renk ailesi, 2-eksenli aile, tek üyeli, kodsuz, supplier-a/b çakışması,
  ileri rev'de kodu KALDIRILAN üye (contracts/supplier-feed-familycode.md listesi).

## Adımlar

1. **Feed çekimi** — `POST /v1/feeds/pull`; pgAdmin: kanonik içerikte FamilyCode dolu;
   kodsuz ürünlerde null.

2. **Merge/çakışma (US1)** — aynı barkoda iki tedarikçi farklı kod verdi: düşük Priority'nin
   dolu değeri kanonikte; tek tarafın verdiği kod kaybolmadı.

3. **Zincir** — Catalog Product.FamilyCode dolu; StorefrontView satırında FamilyCode aynı değer.

4. **Liste tek kart (US3)** — /Products'ta 3-üyeli aile TEK kart; kartta "3 varyant" rozeti;
   toplam kart sayısı = ailesizler + aile başına 1 (SC-003). Temsilci: stokta en ucuz üye.

5. **Filtre-bağlamlı temsilci (FR-009)** — "Renk: Siyah" seç: aile siyah üyeyle temsil;
   facet count'lar kart-bazlı birebir (üç üyeli aile bir sayılır).

6. **Detay seçici (US2)** — aile üyesi detayında Renk seçici; mevcut üye işaretli; başka renge
   tıkla → o üyenin detayı (kendi fiyat/stok/yorum). Stoksuz üye seçilebilir ama soluk.
   2-eksenli ailede iki grup; ailesiz üründe seçici YOK.

7. **Aileden çıkış (SC-005)** — `POST .../advance` ile kodu kaldıran rev'e geç + feed pull:
   ürün aileden düşer; liste ayrı kart çizer; eski ailenin seçicisinde görünmez.

8. **Regresyon (SC-004)** — ailesiz ürün detay/sepet/yorum/kart aynı; 044 yıldız rozeti temsilci
   üyede doğru; sepete ekleme ÜYE ürünle çalışır (aile kavramı sepete sızmaz).

## Beklenen sonuç özeti

| Kontrol | Beklenen |
|---------|----------|
| Merge | Alan-bazlı öncelik; kod değişimi hash'le yeniden yayın |
| Liste | Aile=1 kart + "N varyant"; count/facet kart-bazlı birebir |
| Temsilci | Stokta en ucuz; filtrede eşleşen üyeye kayar |
| Seçici | Ayrışan eksen başına grup; stoksuz ayırt edilir; ailesizde yok |
| Çıkış | Kod kaldırılınca sonraki yayında ailesiz davranış |
| Regresyon | Ailesiz ürünler + sepet/sipariş/yorum değişmedi |

## Testler

```bash
dotnet test tests/Procurement.Api.Tests/Procurement.Api.Tests.csproj   # FamilyCode merge + hash
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj           # upsert + publish alanı
dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj     # temsilci + eksen + facet çekirdekleri
```
