# Research: Ürün Varyantları (045)

## R1 — Feed alanı: opsiyonel `familyCode` (camelCase, satır seviyesi)

- **Decision**: Feed satırına `"familyCode": "PEAK-KLK-1"` opsiyonel string; yokken null/ailesiz.
  Mock rev JSON'ları elle güncellenir (çok üyeli, tek üyeli, kodsuz, tedarikçi-çakışmalı örnekler).
- **Rationale**: 043 `attributes` emsali — opsiyonel alan eski rev'leri kırmaz; mock veri kuralı
  (kod-içi üretici yok) korunur.
- **Alternatives**: Sku/ad kökünden türetme — kırılgan, yanlış gruplama; ELENDİ (kullanıcı kararı).

## R2 — Procurement: FamilyCode = içerik alanı, alan-bazlı Priority-merge + hash'e dahil

- **Decision**: `ListingRow.FamilyCode` (ham) + `CanonicalContent.FamilyCode` (kanonik).
  Merge diğer içerik alanlarıyla aynı: düşük Priority'nin DOLU değeri kazanır (sıra-bağımsız).
  Hash'e (ComputeHash + ContentHash + Equals) girer → kod değişimi yayın tetikler (SC-005).
  `IsComplete`'e GİRMEZ — ailesiz ürün yayını bloklanmaz. Enrich ÜRETMEZ (AI aile uyduramaz).
- **Rationale**: 041 merge sözleşmesi birebir; hash-diff değişim yayılımını bedavaya getirir.
- **Alternatives**: Ayrı aile tablosu/aggregate — spec "gruplama kimliği" diyor, tablo fazlalık; ELENDİ.

## R3 — Kontrat: iki event'e additive `FamilyCode` (default null)

- **Decision**: `CanonicalProductUpserted.FamilyCode` (string?, default null) ve
  `ProductChangedEvent.FamilyCode` (string?, default null). Yeni exchange/kuyruk YOK.
- **Rationale**: 043 Specs emsali — additive alan eski tüketiciyi kırmaz; null = ailesiz.
- **Alternatives**: Ayrı FamilyChanged event'i — tüketici aynı satırı yazıyor, ikinci event
  sıralama/yarış riski ekler; ELENDİ.

## R4 — Catalog: `Product.FamilyCode` (string?, Marten index)

- **Decision**: Kanonik upsert davranış metodu FamilyCode'u yazar; publish yolu event'e koyar.
  Marten index (aile sorguları Storefront'ta ama Catalog agent okumaları için ucuz sigorta).
- **Rationale**: Gtin emsali — feed'den akan opak kimlik alanı.
- **Alternatives**: Catalog'da tutmayıp Procurement→Storefront direkt taşımak — zincir kırılır
  (Storefront'un kaynağı ProductChangedEvent); ELENDİ.

## R5 — Liste gruplaması: filtreleme LINQ'te + gruplama BELLEK-içi (implement'te DISTINCT ON'dan dönüldü)

- **Decision (revize)**: Filtreleme mevcut LINQ/jsonb SQL'de kalır (test edilmiş `ApplyFilters`
  + `ApplySpecFilters`). Filtreli küme materyalize edilip `GroupToRepresentatives` (saf çekirdek)
  aile başına temsilci `(stok>0) DESC, Price ASC, ProductId` seçer, sayfalama bellek-içi. Count =
  aile-anahtarı distinct. FamilyKey = `coalesce(FamilyCode, ProductId)`.
- **Rationale**: DISTINCT ON'lu raw SQL, jsonb spec filtrelerini + sayfalamayı da SQL'e taşımayı
  gerektirir → test edilmiş LINQ çekirdeği kaybolur, tam da 040/043'ün uyardığı kırılganlık geri
  gelir. Ölçek yüzlerce ürün (dev/demo); materyalize + bellek-içi gruplama "mevcut gecikme sınıfı"
  hedefini karşılar ve FR-009 filtre-bağlamlı temsilciyi doğal verir (yalnız filtreyi geçen üyeler
  gruba girer). Temsilci kuralı saf çekirdek + birim testli (SC-003 birebirlik testli).
- **Alternatives**: (a) DISTINCT ON raw SQL — filtre/sayfalama SQL'e taşınır, kırılgan, ELENDİ
  (implement'te denenmeden bu risk görüldü); (b) Projeksiyon-anı IsRepresentative bayrağı — filtre
  bağlamında yanlış, ELENDİ. Ölçek milyonlara çıkarsa DISTINCT ON'a geçiş aday not.

## R6 — Facet sayıları: aile-bazlı distinct (mevcut in-memory yol içinde)

- **Decision**: `GetStorefrontFilterOptions` zaten satırları belleğe alıp sayıyor; sayım anahtarı
  ürün yerine `coalesce(FamilyCode, ProductId)` distinct olur (kategori/marka/spec hepsinde).
- **Rationale**: SC-003 "filtre sayıları görünen kartla birebir" — kart artık aile.
- **Alternatives**: Üye-bazlı count sürsün — kart sayısıyla çelişir (043 birebirlik ihlali); ELENDİ.

## R7 — Detay seçici: yeni `GET /api/v1/storefront/products/{id}/family` + eksen türetme Storefront'ta

- **Decision**: Uç, üyenin FamilyCode'una göre yayınlanabilir üyeleri (dolu-satır filtresi) döner:
  üye listesi (Id, ad, fiyat, stok, görsel, Specs) + türetilmiş eksenler. Eksen türetme SAF statik
  çekirdek (test-first): üyeler arasında FARKLILAŞAN spec attribute'ları eksen olur; hiçbiri
  ayrışmıyorsa eksen boş → seçici üye ADIYLA listelenir (spec edge case). Ailesiz/tek üyeli ürün
  NotFound benzeri boş aile döner → WebApp seçici çizmez.
- **Rationale**: WebApp'e ham hesap taşımamak (iki tüketici doğarsa tekrar); üye sayısı küçük —
  bellek-içi türetme yeterli.
- **Alternatives**: Detay yanıtına gömmek — mevcut detay ucu şişer, aile ayrı yüklenebilir kalsın
  (kart tıkı detayı bekletmesin); ELENDİ.

## R8 — Kapsam sınırı: arama/agent yüzeyi üye-bazlı kalır; WebApp listeleri gruplanır

- **Decision**: Gruplama v1'de `GetStorefrontProductList` (ana sayfa + /Products) + facet +
  detay/family ucundadır. `SearchStorefrontProducts` (agent/MCP + REST) ÜYE-bazlı kalır —
  WebApp'te arama kutusu yok (kanıt: StorefrontService/Refit'te search çağrısı yok).
- **Rationale**: Spec "agent okumaları üye-bazlı sürer" istisnası; kullanılmayan yüzeye gruplama
  eklemek ölü karmaşıklık.
- **Alternatives**: Aramaya da DISTINCT — tüketicisi yok, YAGNI; ELENDİ (aday not: WebApp araması
  gelirse aynı temsilci çekirdeği kullanılır).

## R9 — Kart rozeti: `VariantCount` liste yanıtında

- **Decision**: Liste sorgusu temsilci satırla birlikte ailenin görünür üye adedini döner
  (`VariantCount`; ailesizde 1). Kart `>1` ise "N varyant" rozeti çizer. Rating temsilci üyenindir (044).
- **Rationale**: FR-008 "çeşitlilik hissi" — ikinci sorguya gerek yok, aynı SQL'de window/count.
- **Alternatives**: Rozetsiz tek kart — çeşitlilik kaybolur, FR-008 karşılanmaz; ELENDİ.
