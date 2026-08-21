# Research: Ürün Özellikleri ve Facet Filtre (043)

Tasarım oturumu (2026-08-21) kararları + plan aşamasında çözülen teknik noktalar.

## R1 — Event kontratı: additive Specs alanı, sözleşme AD

- **Decision**: `Shared.IntegrationEvents`'e `ProductSpec(string Attribute, string Option)` record'u;
  `CanonicalProductUpserted` ve `ProductChangedEvent`'e `List<ProductSpec> Specs` alanı (default boş).
  Kanonik ADLAR taşınır (Id değil) — taksonomi sözleşmesiyle aynı desen.
- **Rationale**: Additive alan eski tüketiciyi kırmaz (JSON deserialization yeni alanı yok sayar /
  boş listeyle doldurur). AD sözleşmesi iki BC'nin ayrı seed'ini mümkün kılar (bilinçli tekrar).
- **Alternatives considered**: Ayrı `ProductSpecsChanged` event'i (iki event senkron sorunu doğurur;
  fat-event deseni mevcut); Id taşımak (BC'ler arası Id sözleşmesi taksonomide de reddedilmişti).

## R2 — Feed alanı: opsiyonel sözlük

- **Decision**: `SupplierFeedRow` += `Dictionary<string, string>? Attributes` (opsiyonel; yokluk =
  eski davranış). Mock rev JSON'larına elle örnekler eklenir (Renk/Materyal/Garanti/Enerji).
- **Rationale**: Opsiyonellik eski rev dosyalarını geçerli bırakır; sözlük ham tedarikçi anahtarını
  korur ("COLOR"→"BLACK"), eşleme Procurement'ta yapılır.
- **Alternatives considered**: Tipli alanlar (renk/beden ayrı property — tedarikçi çeşitliliğini
  modelleyemez); zorunlu alan (tüm rev dosyaları elden geçerdi).

## R3 — Procurement: registry + eşleme + merge

- **Decision**: `Seeding/CanonicalSpecs.cs` statik tanımlar: `SpecDefinition(Name, Filterable,
  DisplayOrder, Options[])` (4 attribute: Renk, Materyal, Garanti Süresi, Enerji Sınıfı) +
  tedarikçi-başına `SpecValueMapping(RawKey, RawValue, Attribute, Option)` listeleri (supplier-a
  Türkçe, supplier-b İngilizce anahtarlar — kategori-eşleme ikizi). `SupplierListing` ham
  `RawAttributes` sözlüğünü saklar; `RebuildCanonical()` attribute-başına merge eder: aktif
  listing'ler Priority sırasında, eşlenmiş İLK dolu değer kazanır (sıra-bağımsız). Eşlenemeyen ham
  anahtar YOK SAYILIR (spec edge case).
- **Rationale**: Mevcut merge deseninin (alan-bazlı) attribute'a genellenmesi; seed mekanizması
  `ProcurementSeedHostedService`'te hazır.
- **Alternatives considered**: Eşlemeyi DB tablosu yapmak (admin ekranı yok — statik seed yeter);
  tüm-set-kazanır merge (tedarikçi A renk verirken B materyal veriyorsa ikisi de kazanmalı —
  attribute-başına şart).

## R4 — Enrichment: kapalı listeden spec seçimi

- **Decision**: `EnrichmentOutput` += `List<SpecPick(Attribute, Option)>? Specs`. Prompt'a eksik
  attribute listesi + her birinin kapalı option listesi girer; talimat "yalnız listeden seç,
  bilemiyorsan boş bırak". Guard: `PoolProduct.ApplyEnrichment` registry'de olmayan attribute/option
  çiftini REDDEDER (kategori guard'ının ikizi) — satır spec'siz ilerler, akış durmaz (FR-004).
  Spec eksikliği enrich tetikleyicisine DAHİL (SourceHash'e eksik-attribute listesi girer) ama
  yayını bloklamaz (FR-005 — CanonicalContent.Status spec'e bakmaz).
- **Rationale**: Mevcut kategori-seçim deseninin birebir genişlemesi; Temperature=0 + structured
  JSON zaten kurulu.
- **Alternatives considered**: Spec eksiğini enrich'e sokmamak (AI değeri kaybolur — D kararının
  yarısı düşerdi); serbest değer + sonradan normalize (kapalı-liste kuralını deler).

## R5 — Catalog modeli: seed'li aggregate + Product-içi atama

- **Decision**: `SpecificationAttribute` aggregate: `Name`, `NormalizedName` (unique index),
  `Filterable`, `DisplayOrder` + child `SpecificationAttributeOption` entity listesi (Id+Name+
  DisplayOrder; AddOption guard: boş/mükerrer ad). Seed: `CatalogSpecSeedHostedService`
  (get-or-create, taksonomi emsali). Product: `_specifications` listesi —
  `ProductSpecificationAssignment(AttributeId, OptionId)` record VO (`ProductValueObjects.cs`'e);
  `SetSpecifications(IReadOnlyList<...>)` davranışı tam-değiştirme yapar (upsert tek yol; mükerrer
  attribute guard'ı). REST penceresi: List + Create + AddOption uçları (ProductTag emsali).
- **Rationale**: İlke II Id-referans kuralı (atama Id'lerle); atama ayrı aggregate olsaydı upsert
  akışı ikinci yazım yolu doğururdu — kategori-atama emsali Product-içi.
- **Alternatives considered**: Staging'deki gibi ayrı `ProductSpecificationAttribute` aggregate
  (nopCommerce admin-akışı için anlamlıydı; bizde upsert tek yol — gereksiz); atamada ad saklamak
  (İlke II ihlali; adlar event'e handler'da registry'den çözülür).

## R6 — Storefront: SpecKeys düzleştirmesi + MatchesSql kesişimi

- **Decision**: `StorefrontView` += `List<SpecPair> Specs` (ad çiftleri — facet + detay için) ve
  `string[] SpecKeys` ("Attribute|Option" düz anahtarları — sorgu için). Filtre uygulaması:
  attribute-başına grup → grup içi VEYA jsonb `?|` (herhangi biri) operatörüyle
  `MatchesSql("d.data -> 'SpecKeys' ??| :keys", ...)`, gruplar arası VE (zincirli Where).
  Facet: mevcut `BuildOptions` satırları zaten belleğe çekiyor — spec sayımları aynı geçişte
  (`Filterable` bilgisi event'ten değil, sayfada gösterilecek attribute sırası registry'den değil
  SATIRDAN türetilir; Filterable=false spec'ler event'e hiç KONMAZ → facet doğal temiz kalır,
  detay tablosu için ayrı `DisplaySpecs`... — hayır, sadeleştirme: Filterable=false MVP seed'inde
  yok; tüm seed attribute'ları filterable. Detay tablosu Specs listesini gösterir).
- **Rationale**: Marten LINQ'da dinamik iç-içe Any/OR kompozisyonu kırılgan (040 MatchesSql dersi);
  düz anahtar dizisi + jsonb operatörü hem basit hem hızlı. Dev ölçeğinde bellek-içi facet sayımı
  yeterli (satırlar zaten çekiliyor).
- **Alternatives considered**: LINQ expression-tree kompozisyonu (Marten çeviri riski); ayrı
  facet tablosu/projeksiyon (YAGNI — satır sayısı küçük); Filterable=false'u satıra taşımak
  (MVP seed'i hep filterable — alan şimdilik registry'de durur, satıra sızmaz).

## R7 — WebApp: checkbox facet + query-string

- **Decision**: Sol panel: attribute başlığı altında option checkbox'ları + ürün sayısı;
  `<form method="get">` mevcut deseni sürer. Query-string: `spec=Renk|Siyah&spec=Materyal|Çelik`
  (çoklu `spec` anahtarı; `|` ayracı — ad'larda kullanılmayan karakter). Seçimler sayfalama +
  kategori/marka paramlarıyla birlikte taşınır; "Temizle" mevcut butona dahil.
  Detail: fiyatın altına "Özellikler" tablosu (Specs boşsa bölüm render edilmez).
- **Rationale**: Mevcut select-onchange deseninden checkbox'a geçiş şart (çoklu seçim); GET formu
  URL-taşınabilirlik gereksinimini (edge case) bedavaya çözer.
- **Alternatives considered**: JS ile dinamik filtre (mevcut sayfa deseni sunucu-render; gerek yok);
  virgüllü tek param (aynı attribute çoklu seçimde ayrıştırma bulanır).

## R8 — Cache ve 042 etkileşimi

- **Decision**: `filters` tag'li cache aynen — `ProductChangedEvent` handler'ı zaten invalidate
  ediyor; spec değişimi de aynı event'le geldiğinden ek iş yok. Liste sorgusu cache'lenmiyor
  (kardinalite kuralı — spec kombinasyonları bunu pekiştirir, `[Cached]` EKLENMEZ). 042 davranış
  logu: filtreli liste mevcut ListShown impression'ını basmaya devam eder — değişiklik yok.
- **Rationale**: Mevcut kurallar (cache kardinalite ölçütü) doğrudan uygulanıyor.
- **Alternatives considered**: Facet yanıtına ayrı tag (gerek yok — aynı veri kaynağı).
