# Contract: Tedarikçi Feed'leri

Üç tedarikçi, üç biçim, İKİ transport (R17): acme+nordic HTTP ucu (anonim), tekno DOSYA-bırakma.
Full snapshot döner (FR-003). Veriler temiz/tekdüzedir: nokta ondalık, temiz marka adları.
İndirim alanları her şemada tanımlı ama boş bırakılabilir (opsiyonel).

acme/nordic kanonik verisi `Supplier.Api/Datasets/*.json`'dadır; uçlar istek anında okuyup kendi biçiminde
render eder (DB yok, R12). tekno'nun kaynağı doğrudan `data/supplier-drops/tekno.xml` dosyasıdır.

## acme — JSON API

`GET /v1/feeds/acme` → `200 application/json`. Markalar: Apple, Samsung, Sony.

```json
[
  {
    "productId": "ACM-1001",
    "title": "iPhone 15",
    "description": "Apple akıllı telefon",
    "brand": "Apple",
    "price": 999.90,
    "quantity": 25,
    "discountCode": "SUMMER25",
    "discountPercent": 10
  },
  { "productId": "ACM-1002", "title": "Galaxy S24", "description": "...", "brand": "Samsung",
    "price": 899.00, "quantity": 30, "discountCode": null, "discountPercent": null }
]
```

## nordic — CSV dump

`GET /v1/feeds/nordic` → `200 text/csv`. `;` ayraçlı, başlık satırlı. Markalar: Nike, Adidas.
İndirim kolonları boş bırakılabilir.

```csv
ext_id;name;desc;brand;price;stock;disc_code;disc_pct
NRD-2001;Air Max 90;Koşu ayakkabısı;Nike;129.90;40;;
NRD-2002;Ultraboost;Koşu ayakkabısı;Adidas;149.90;35;SPOR15;15
```

## tekno — XML dosya-bırakma (HTTP ucu YOK, R17)

Kaynak: `data/supplier-drops/tekno.xml` (IngestionAgent config'iyle bulunur; yol AppHost'tan env ile gelir).
`<discount>` elemanı opsiyoneldir. Markalar: Lenovo, Dell, Hp, Asus, Xiaomi. Dosya yoksa tedarikçi `Unreachable`.

```xml
<products>
  <product>
    <code>TKN-3001</code>
    <name>ThinkPad X1</name>
    <details>İş dizüstü bilgisayarı</details>
    <manufacturer>Lenovo</manufacturer>
    <price>1899.00</price>
    <stockCount>10</stockCount>
    <discount code="TECH5" percent="5" />
  </product>
  <product>
    <code>TKN-3002</code>
    <name>XPS 13</name>
    <details>Ultrabook</details>
    <manufacturer>Dell</manufacturer>
    <price>1499.00</price>
    <stockCount>8</stockCount>
  </product>
</products>
```

## Ortak kurallar

- Üç biçim de staging'e aynı şekilde iner: kayıt parçası ham string olarak `RawPayload`'a, parse edilmiş hali
  ortak `FeedRecord` modeline (adapter işi).
- Harici kimlik feed içinde benzersizdir; mükerrer gelirse ingestion ilkini işler, kalanı Failed sayar.
- Alan → ara model eşlemesi ilgili adapter'ın işidir; eşleme adapter birim testlerinde birebir doğrulanır.
- Bozuk kayıt simülasyonu (US4): veri setinde eksik alan (boş name/brand) veya geçersiz indirim yüzdesi.
- Feed boş ise geçerli yanıttır (boş liste/dosya/kök eleman); ingestion `Empty` olarak raporlar.