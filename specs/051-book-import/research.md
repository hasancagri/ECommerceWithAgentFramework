# Research: First-Party Kitap Toplu Import

Phase 0 — spec + tasarım sohbetinden çözülen kararlar. Dataset canlı profillendi (scratchpad `raw.json`).

## D1 — İş ayrımı: şekillendirme (İş1) vs yazım (İş2)

**Karar:** İş1 = build-time Python script (`scripts/book-import/shape_books.py`), Catalog DIŞI; ham 20MB dataset
repoya girmez, süzülmüş küçük `books.json` üretir + commit edilir. İş2 = Catalog açılış seeder'ı ham veriyi
görmez, yalnız `books.json` okur (ince kalır).
**Gerekçe:** Ağır parse/dedup/kur-çevirme Catalog aggregate'inin işi değil. Repo şişmesin. [[mock-data-as-json-files]] deseni.
**Alternatif red:** Catalog'un ham dataset'i okuyup ayıklaması — şişman senkron importer, BC'yi kirletir.

## D2 — Yeni ingestion servisi AÇILMAZ

**Karar:** Import Catalog içinde; ayrı servis yok. Supplier-benzeri BC reddedildi.
**Gerekçe:** Yeni ingestion servisi ancak 4'ü birden varsa haklı: süreklilik + dış-kaynak(ACL) + kendi-invariant'ı
+ operasyonel-izolasyon. One-shot first-party seed hiçbirini sağlamıyor. Supplier+Procurement 050'de tam bu
sebeple söküldü ([[050-first-party-pivot]]); geri getirmek "Yapma listesi" ihlali.
**Alternatif red:** Storefront'a import edip diğerlerini dinletmek — read-model rebuild'de seed silinir,
publish-gate baypas, push-only rolü kırılır, kimlik otoritesi Catalog'da değil.

## D3 — Deterministik ProductId

**Karar:** ProductId = ISBN'den deterministik GUID (namespace-based, RFC-4122 v5 tarzı). `AggregateRoot.Id`
`public set` olduğu için import aggregate Id'sini deterministik atar.
**Gerekçe:** Idempotency anahtarı + servisler-arası ortak anahtar (Stock barkod↔id, Storefront satır). Aynı
ISBN her çalıştırmada aynı id → re-run upsert, çoğaltma yok.
**Alternatif red:** Rastgele `Guid.NewGuid()` (CreateProduct'ın bugünkü davranışı) — re-run'da idempotency
için ayrı Gtin-lookup gerekir, ekstra sorgu + yarış.

## D4 — Publish-gate = yalnız fiyat>0 (aggregate invariant)

**Karar:** `Product.Publish()`'a guard: `Price.Amount > 0` değilse `ResultDomain.Error(PRODUCT_PRICE_REQUIRED_FOR_PUBLISH)`.
Kapak ve açıklama yayın için zorunlu DEĞİL. Kapaksız kitap yayınlanır, vitrin placeholder gösterir.
**Gerekçe:** Fiyatsız = satılamaz (kırık kart). Kapak kozmetik; satılabilir kitabı gizleme. Kapı aggregate'te
(İLKE II: "yeni kural → aggregate metoduna bak, handler'a değil"). 001'de konup 010'da sökülen tamlık-kapısının
bilinçli, gerekçeli dönüşü (first-party + kısmi veri = artık gerçek ihtiyaç).
**Alternatif red:** Kapı = fiyat+kapak (kullanıcı reddetti — kapaksızı gizlemek istemiyor). Kapı handler'da
(İLKE II ihlali, kalıcı değil, sonra fiyat gelince kendiliğinden yayınlanmaz).
**Veri etkisi:** ISBN kümesinde (1429) fiyat eksik yalnız **34** (memory'deki "868" YANLIŞ paydaydı — tüm 2269
üzerinden). 34 kitap taslak kalır; ~1393 yayınlanır (12 kapaksız dahil, placeholder ile).

## D5 — Taksonomi kitap VERİSİNDEN türetilir; eski demo seeder'lar silinir

**Karar:** `CatalogTaxonomySeedHostedService` (Elektronik/Moda) ve `CatalogSpecSeedHostedService` (Renk/Beden
specs) SİLİNİR (kayıt Program.cs'ten kalkar). Kategori/marka import sırasında get-or-create edilir.
**Gerekçe:** Elektronik demo taksonomisi kitapçıyla alakasız (pre-pivot artık). Kitapların spec-attribute'u yok
(`SetSpecifications` opsiyonel, CreateProduct zorlamıyor). Testlerde bu seeder'lara referans yok → güvenli silme.
**Doğrulama:** `grep TaxonomySeed|SpecSeed tests/` → 0 sonuç.

## D6 — Kategori: 2-seviye tür ağacı (mid > leaf)

**Karar:** `categories` dizisinden "Books" sabiti atılır; `[1]=mid` (parent Category) > `[2]=leaf` (child Category,
primary atama). Get-or-create, NormalizedName teklik (mevcut 016 düzeni). Leaf = ürünün primary kategorisi.
**Gerekçe:** Dataset %100 dolu, 1426 kitap tam 3 seviye (3 kitap 4 seviye — `[1]`/`[2]` alınır), leaf-eksik sıfır.
30 mid + 126 leaf + 141 (mid>leaf) çift → gerçek genre facet'i Storefront'a. Category parent/child modeli hazır.
**Alternatif red:** Tek "Kitap" kategorisi (facet kaybı) / düz leaf (parent hiyerarşisi kaybı). Veri 2-seviyeyi
bedavaya veriyor.
**Kabul:** Tür adları İngilizce kalır (dataset kaynaklı); Türkçeleştirme ertelenir (sonraki iş).

## D7 — Brand = dataset `brand` alanı (verbatim, get-or-create)

**Karar:** `Product.Brand` ← dataset'in `brand` alanı **olduğu gibi**; "yazar" olarak yorumlanmaz, first-class
Author kavramı kurulmaz. Her tekil `brand` (680 tekil, 1428/1429 dolu) `Brand.Create(name)` ile idempotent upsert;
NormalizedName teklik. Import ürünü bu BrandId'ye bağlar.
**Gerekçe:** ProductChangedEvent + CreateProduct BrandId zorunlu (016). Dataset'te brand-benzeri tek gerçek alan
budur (`manufacturer`/`upc`/`department`≈boş; `seller_name`=pazaryeri reseller, first-party'de anlamsız).
İçeriği ağırlıkla yazar adı + ~32 yayınevi (Workman/Scholastic) — karışık, umursanmaz, verbatim alınır.
**Kabul:** ~73 "by ..." önekli değer İş1'de hafif kırpılabilir (opsiyonel); ortak-yazar concat bölünmez.
**Yazar-first-class REDDEDİLDİ:** Author aggregate + M:N + event değişimi = ayrı feature büyüklüğü; adım 2
"aynı yazar" önerisi Brand-gruplamasıyla zaten yürür. Gerekince (yazar sayfası/disambiguation) sonra promote edilir.

## D8 — Fiyat: tek `final_price`, USD→sabit kur→TL (İş1'de)

**Karar:** `Price = final_price` (yoksa `initial_price`; ikisi de yoksa boş→taslak). İş1 USD→TL sabit kur ile
çevirir; `books.json` TL taşır. Catalog TL alır, kur mantığı taşımaz.
**Gerekçe:** Fiyat ISBN kümesinde %98 dolu. Canlı kur yok (kullanıcı kararı). Compare-at/liste fiyatı (`initial_price`,
`discount`) bu feature'a GİRMEZ — kullanıcı "Discount'u boşver" dedi; indirim ayrı BC (adım 4).
**Kabul:** Sabit kur değeri İş1 script'inde sabit (dokümante; ör. 1 USD = 40 TL — keyfi, sonra ayarlanır).

## D9 — Import mekanizması: HostedService → ImportBook command

**Karar:** `BookImportHostedService` (IHostedService, mevcut seeder deseni) `books.json` okur; her kitap için
`IMessageBus.InvokeAsync(new ImportBook.Command(...))`. Command handler: get-or-create Brand+Category, deterministik
id upsert, `Publish()` gate, event yayımı (Wolverine outbox).
**Gerekçe:** Domain mantığı handler'da (HostedService ince). Wolverine outbox event'i güvenli yayar. Idempotent
(re-run aynı id upsert). CreateProduct yeniden kullanılmaz — deterministik id + upsert + ProductAdded + gate onda yok.
**Alternatif red:** Seeder doğrudan IDocumentSession'a yazsın — event yayımı + aggregate davranışı handler'da olmalı.

## D10 — ProductLinked → ProductAdded rename + omurga uyanışı

**Karar:** `ProductLinked` her yerde `ProductAdded` (record, RabbitMqConstants class, exchange `catalog.product-added`,
queue `stock.product-added`, Stock handler + Program). Yayınlanan üründe `ImportBook` handler `ProductAdded`
(barkod=ISBN, ProductId, InitialStock) yayar → Stock ilk OnHand.
**Gerekçe:** "Linked" feed-kaynaklı ad, first-party'de anlamsız (kullanıcı reddetti). Omurga 050'de dormant kaldı;
bu feature ilk yayıncısını (import) verince uyanır.
**InitialStock:** sabit varsayılan **100** (dataset güvenilir adet taşımaz; `availability` serbest-metin, parse yok).